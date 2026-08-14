# Client traffic isolation and channel-switch handling

## Problem

Live capture records traffic that is not ours, and loses its footing when the
character changes channel.

**Foreign traffic.** The BPF filter is built as
`tcp and src host {serverIp} and src port {serverPort}`
(`CaptureCommand.cs`, `LiveSessionFactory.cs`, `ServeCaptureCommand.cs`). It
constrains only the source — the game server — and says nothing about the
destination. Any machine or process on the capture segment talking to that same
server matches. `LiveFrameSource` opens the device in promiscuous mode, so those
frames are visible in the first place, and `PacketPipeline` keys reassemblers by
full 5-tuple, so a foreign stream is decoded as a well-formed second flow rather
than being noticed as an intruder.

The reported case is a NAT'd virtual machine running its own client. Its packets
leave through the host's NAT service, so on the wire they carry the host's IP and
are indistinguishable from ours by address alone. Only the TCP table, which maps
a local port to the process owning it, separates them: the VM's port belongs to
the hypervisor's NAT service, not to `Client.exe`.

**Channel switching.** The filter is pinned once at startup from a single
`TryResolveOnce()`. A channel switch opens a new socket, often to a different
server address in the same /24 (both `210.208.80.41:11022` and
`210.208.80.34:11022` have been observed), and the pinned filter stops matching.
Capture goes silent with no error.

## Prior art

mogugi (`lib/pcaputil`, `lib/packet/gamePacketReader.go`) solved both. Four
details of its implementation shape this design:

1. **Vetting is per stream, not per packet.** The verdict is cached in the
   stream's state at first sight and never revisited:

   ```go
   st := streams[key]
   if st == nil {
       if t.vetPort != nil && !t.vetPort(fmt.Sprintf("%d", uint16(tcp.DstPort))) {
           streams[key] = &tcpStreamState{rejected: true}
           continue
       }
       ...
   }
   if st.rejected { continue }
   ```

   The TCP table is therefore consulted once per new connection, not once per
   frame.

2. **Recording sits downstream of the vet**, with the reason stated outright:
   *"Record only accepted streams (after the vet, so foreign traffic never lands
   in the pcapng)."*

3. **The filter is updated live**, never by rebuilding the reader:
   `handle.SetBPFFilter(filter)`.

4. **A new connection opens with a 4-byte encryption key** that carries no game
   data and must be skipped before framing begins.

mogugi also fails open when the TCP table cannot be read — *"better to record
extra traffic than lose game data"* — which this design adopts.

Where this design departs: mogugi no longer reports channel switches (its
`"channel_switch"` reason is emitted nowhere; the frontend string is vestigial).
It optimises for a damage meter, where a switch should be invisible. This project
produces NDJSON that is read afterwards, so a switch is context worth recording.

## Design

### Data flow

```
LiveFrameSource            BPF: tcp and src net <union of server /24s>
  └→ ClientTrafficFilterSource        per-stream local-port vet
       ├→ FrameRecorder / SessionRecorder   → pcapng
       └→ PacketPipeline                    → decode

CaptureSession (500ms poll)
  ├→ server /24 set changed → LiveFrameSource.SetFilter
  └→ endpoint changed       → SessionEvent.ConnectionLost / ConnectionResumed
```

The decorator's position between the source and the recorder is the load-bearing
part: it is what keeps foreign frames out of the pcapng, not merely out of the
decoded output.

### Components

**`ClientConnectionTracker`** (`Core/Capture`, new) — the polled view of
`Client.exe`'s connections, wrapping `ITcpConnectionTable`.

- `IsClientLocalPort(ushort port)` — set of the client's local ports, cached for
  2 seconds (mogugi's value). A miss re-polls once before answering, so a socket
  opened seconds ago is admitted without waiting for the next scheduled poll.
  Returns true when the table cannot be read.
- `ServerNetworks()` — the /24 of each current game connection, excluding ports
  80 and 443.
- Thread-safe: the capture callback and the watchdog both call it.

**`ClientTrafficFilterSource`** (`Core/Sources`, new) — an `IFrameSource`
decorator. Extracts `(srcIp, srcPort, dstPort)` as a stream key, decides once per
stream whether `dstPort` belongs to the client, caches the verdict, and re-raises
`FrameReceived` only for accepted frames.

**`BpfFilter`** (`Core/Capture`, new) — pure function from a connection list to
`tcp and (src net a.b.c.0/24 or ...)`. Deduplicates, orders deterministically so
an unchanged set produces an unchanged string, and skips web ports.

**`LiveFrameSource.SetFilter(string)`** — assigns `_device.Filter` on the running
handle. Rebuilding the source instead would break the `FrameReceived`
subscriptions that the recorder and the pipeline hold.

**`CaptureSession`** (existing, given the watchdog role) — currently dead code:
it already emits `ConnectionLost` / `ConnectionResumed`, but nothing constructs
it outside its own tests. It gains a 500ms poll (mogugi's cadence) that keeps the
filter equal to the client's server networks and reports endpoint changes.

**4-byte key skip** (`PacketPipeline`) — when a flow's first payload is exactly
4 bytes, it is the connection's encryption key: skip it instead of framing it.
The Mabinogi header is 6 bytes, so a 4-byte payload cannot be a packet.

### Error handling

| Failure | Behaviour |
|---|---|
| TCP table unreadable | Accept the frame (fail open) |
| `SetFilter` throws | Log, keep the previous filter, retry on the next poll |
| Watchdog throws | Caught and logged; capture continues |

### Known limitations

- Attaching to a connection already in progress may see a 4-byte payload that is
  not the key, costing one resync. Self-correcting, and the same risk mogugi
  accepts.
- Fail-open means a TCP-table outage briefly readmits foreign traffic.
- The per-stream verdict is never revisited, so a local port reused by a
  different process within one session keeps its original verdict.

## Testing

| Unit | Covers |
|---|---|
| `ClientConnectionTracker` | caching, re-poll on miss, fail open |
| `ClientTrafficFilterSource` | accept/reject; the table is consulted once per stream |
| `BpfFilter` | /24 derivation, web ports excluded, dedup, stable ordering |
| `CaptureSession` | filter-change and endpoint-change events |
| `PacketPipeline` | leading 4-byte key skipped, following packet still decodes |
| integration | client and VM frames into one source; only client frames reach the recorder |

The integration test is the one that answers the original complaint, so it
asserts at the recorder, not at the decoder.

## Files

New: `ClientConnectionTracker.cs`, `ClientTrafficFilterSource.cs`, `BpfFilter.cs`

Changed: `LiveFrameSource.cs`, `CaptureSession.cs`, `PacketPipeline.cs`,
`CaptureCommand.cs`, `LiveSessionFactory.cs`, `ServeCaptureCommand.cs` — all
three live entry points wire the decorator and the watchdog.

## Out of scope

- Replay: the decorator and watchdog are live-only.
- Recording the client's outbound traffic; capture stays receive-direction, as it
  is today and as mogugi does.
