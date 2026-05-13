# Mabipacade

Mabinogi network packet parser — C# library + CLI sidecar + WebSocket server + WPF debug GUI.

Captures Mabinogi's server → client TCP traffic from your NIC, peels off the outer framing and Message body, then feeds it to 24 op-level decoders that produce strongly-typed POCOs.

Windows-only, relies on Npcap to capture.

> Chinese version: [README.md](README.md)

```
        ┌──────────────┐
        │ Mabinogi     │
        │ client (Win) │
        └──────┬───────┘
               │ TCP
        ┌──────▼───────┐
        │ Npcap + NIC  │
        └──────┬───────┘
               │ raw frames
        ┌──────▼─────────────────────────────────────┐
        │  Mabipacade.Core (pipeline)                │
        │  TCP reassembly → MabiPacketFramer →       │
        │  MessageElemReader → DecoderRegistry       │
        └──┬────────────┬────────────┬───────────────┘
           │            │            │
       MabiPacket   SessionEvent   PcapWriter
           │            │            │
   ┌───────┼────────────┼─────────┐  └─→ session.pcap
   │       │            │         │
   ▼       ▼            ▼         ▼
  Cli   Server      DebugUi      other .NET
 NDJSON  ws://      WPF GUI      consumers
  to     :9876                   (events subs)
 stdout
```

---

## Who it's for

| Role | Use this |
|---|---|
| **.NET developers** building damage meters, boss notifiers, etc. | `Mabipacade.Core` + `Mabipacade.Decoders` (events API) |
| **Go / Python / JS** tools that want a packet stream | `Mabipacade.Cli` (subprocess + stdout NDJSON) |
| **Browser frontend** or multi-consumer scenarios | `Mabipacade.Server` (WebSocket) |
| Watching packets, hunting bugs, finding undocumented ops | `Mabipacade.DebugUi` (WPF live + replay) |

---

## Requirements

- **Windows** (Mabinogi is Windows-only, so the toolchain isn't planned cross-platform)
- **.NET 10 SDK**
- **[Npcap](https://npcap.com/)** — native capture library. During install tick *"Install Npcap in WinPcap API-compatible Mode"*
- Pcap replay mode doesn't need Npcap at runtime, but SharpPcap still tries to load it at startup — install anyway

---

## Download

Latest release: <https://github.com/irusan-fanclub/mabipacade/releases>

Unzip and place the three exes anywhere — they don't write to the registry. Full steps in [INSTALL.md](INSTALL.md).

Build from source:
```powershell
git clone https://github.com/irusan-fanclub/mabipacade.git
cd mabipacade
dotnet test
.\scripts\publish.ps1
```
Outputs land in `artifacts/`.

---

## Quick Start

### 1. WPF GUI (most complete experience)

```powershell
dotnet run --project src/Mabipacade.DebugUi
```

Once the window opens:
- Click **Open pcap…**, pick a pcap → press ▶ to replay (default is Max speed = bulk load; for visual playback pick 1x or 0.5x)
- Click **Start Live** → auto-finds `Client.exe`, resolves the game server endpoint, opens Npcap capture
- The center DataGrid shows packets; the right side tabs show Decoded JSON / Elems / Hex / Names
- The status bar shows ● Live / ▶ Replay / ○ Stopped
- Name resolution: edit `settings.json` (next to the `.exe`) and add `"xmlDataDirectory": "..."`. Two layouts work:
  - **Flat** — `SkillInfo.xml` + `SkillInfo.taiwan.txt` directly in the given directory
  - **Extracted** — point at the root of an extracted `.it` pack; the loader picks up `data/db/Skill/SkillInfo.xml` + `data/local/xml/SkillInfo.taiwan.txt`

### 2. CLI sidecar (for Go / Python / JS consumers)

```powershell
# Replay a pcap
mabipacade replay --in tests/Mabipacade.Core.Tests/fixtures/known_good.pcap

# Live capture
mabipacade capture --region tw --filter-op 0x7926,0x6984

# Capture + record session
mabipacade capture --record-pcap ./sessions
```

stdout = one JSON object per line (NDJSON), stderr = log. Go example:

```go
cmd := exec.Command("mabipacade.exe", "capture", "--region", "tw")
stdout, _ := cmd.StdoutPipe()
cmd.Start()
scanner := bufio.NewScanner(stdout)
for scanner.Scan() {
    var env struct {
        Kind string `json:"kind"`
        Op   string `json:"op,omitempty"`
        Type string `json:"type,omitempty"`
    }
    json.Unmarshal(scanner.Bytes(), &env)
    switch env.Kind {
    case "packet":
        // handle packet by Op or Type
    case "event":
        // ConnectionLost / Resumed / SessionStart / SessionEnd ...
    }
}
```

### 3. WebSocket server

```powershell
mabipacade-server replay --in sessions/known_good.pcap --port 9876
mabipacade-server capture --region tw --port 9876
```

Browser:

```javascript
const ws = new WebSocket('ws://127.0.0.1:9876')
ws.onmessage = e => console.log(JSON.parse(e.data))
// subscribe with a filter
ws.send('{"op":"subscribe","kinds":["packet"],"ops":["0x7926","0x6984"]}')
```

### 4. C# library

```csharp
using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Sources;
using Mabipacade.Decoders;
using Mabipacade.Decoders.Skills;

using var source = new PcapFileFrameSource("session.pcap");
var registry = new DecoderRegistry();
DefaultDecoders.RegisterAll(registry);
var pipeline = new PacketPipeline(source, registry);

pipeline.PacketReceived += (_, p) =>
{
    if (p.Decoded is PlayerSkillPrepareStart prep)
        Console.WriteLine($"skill prep {prep.SkillId}");
};
pipeline.SessionEventReceived += (_, e) =>
{
    if (e is SessionEvent.ConnectionResumed { SameAsLast: false })
        Console.WriteLine("Server changed — reset local state");
};

await pipeline.StartAsync(CancellationToken.None);
```

---

## NDJSON Schema

stdout and WebSocket use the same schema. One JSON object per line; `kind` is the discriminator.

### Packet

```json
{
  "kind": "packet",
  "ts": "2026-05-13T08:23:11.842Z",
  "dir": "in",
  "op": "0x6984",
  "opName": "PlayerSkillPrepareStart",
  "entityId": "12345678901234",
  "type": "PlayerSkillPrepareStart",
  "decoded": {"skillId": 59000},
  "elems": [{"t":"Short","v":59000}]
}
```

Key fields:

| Field | Notes |
|---|---|
| `op` | Always a hex string `"0xXXXX"` — don't use decimal (enum values would mismatch) |
| `entityId` | uint64 as **string** (JS number loses precision above 2^53) |
| `opName` | Only populated when the OpCodes enum has a match; otherwise `null` |
| `type` | L3 decoder POCO class name (e.g. `PlayerSkillPrepareStart`); `null` if no decoder |
| `decoded` | Serialized POCO, camelCase; `null` if no decoder |
| `elems` | L2 elem list, **always present**; `t` = 1..7 type string, `v` = value (uint64 as string) |
| `dir` | `"in"` / `"out"` — always `"in"` in v1; consumers should still check, not assume |

### Event

```json
{"kind":"event","ts":"...","type":"SessionStart","region":"tw","processId":4812}
{"kind":"event","ts":"...","type":"ConnectionLost","lastRemote":"61.218.1.2:11000"}
{"kind":"event","ts":"...","type":"ConnectionResumed","newRemote":"61.218.1.3:11000","sameAsLast":false}
{"kind":"event","ts":"...","type":"BadBody","op":"0x9093","length":42}
```

8 event types: `SessionStart` / `SessionEnd` / `ConnectionEstablished` / `ConnectionLost` / `ConnectionResumed` / `FrameResync` / `BadBody` / `DecoderFailed`.

**`SameAsLast`**: `true` = brief reconnect to the same endpoint, state can usually be kept; `false` = server/channel change, state should be reset.

### Session recording files

`mabipacade capture --record-pcap <dir>` and `Mabipacade.DebugUi` both produce these three sidecar files:

```
sessions/
└── 2026-05-13T18-23-11/
    ├── session.pcap            # raw frames (openable in Wireshark)
    ├── session.events.ndjson   # SessionEvent stream
    └── session.json            # session metadata + stats
```

---

## 24 Built-in Decoders

Drawn from `D:/Projects/Notes/mabinogi-packet-decoding/README.md` (verified ops):

| Category | Ops | POCO |
|---|---|---|
| Combat | `0x7924` `0x7925` `0x7926` | `CombatAction`, `CombatActionEnd`, `CombatActionPack(AttackerId, Sub[])` |
| Skills | `0x6984` `0x6985` `0x6988` `0x6989` `0x698B` `0x6993` | `PlayerSkillPrepare*`, `PlayerSkillPostCastAck*`, `PlayerSkillStop` |
| Entity | `0x520C` `0x520D` `0x5334` `0x5335` `0x53FC` | `EntityAppear(RaceId, Name)`, `EntityDisappear`, `EntitiesAppear/Disappear`, `IsNowDead` |
| Stats | `0x7530` `0x7532` `0x7534` `0xA028` | marker types (body shape pending verification) |
| Misc | `0x526C` `0x9091` `0x9095` `0xA41E` `0xA43C` `0x59E6` | `Chat(Sender, Message)`, `Effect`, `SharpMind`, `EquipmentChanged`... |

⚠️ Most marker types don't decode their body yet — seeing the packet arrive with an empty POCO is expected behavior.

### Adding your own decoder

```csharp
public sealed record MyEvent(ushort SkillId, ulong TargetId);

public sealed class MyDecoder : IPacketDecoder
{
    public ushort Op => 0xABCD;
    public object Decode(DecoderInput input) =>
        new MyEvent(
            SkillId: input.Elems[0].AsUInt16(),
            TargetId: input.Elems[1].AsUInt64());
}

var registry = new DecoderRegistry();
DefaultDecoders.RegisterAll(registry);   // 24 built-in
registry.Register(new MyDecoder());      // your own
```

---

## Project structure

```
mabipacade/
├── src/
│   ├── Mabipacade.Core/         # Pipeline, capture, recording, replay, JSON
│   ├── Mabipacade.Decoders/     # 24 L3 decoders + OpCodes + OpCodeNames
│   ├── Mabipacade.Cli/          # NDJSON sidecar (mabipacade.exe)
│   ├── Mabipacade.Server/       # WebSocket server (mabipacade-server.exe)
│   └── Mabipacade.DebugUi/      # WPF GUI
├── tests/
│   ├── Mabipacade.Core.Tests/         # 64 unit + 2 fixture-skipped E2E
│   ├── Mabipacade.Decoders.Tests/     # 53 per-decoder tests
│   ├── Mabipacade.Cli.Tests/          # 34 + 1 fixture E2E
│   ├── Mabipacade.DebugUi.Tests/      # 46 + 1 env-skipped (live)
│   └── Mabipacade.Server.Tests/       # 4 subscription tests
├── docs/
│   └── superpowers/
│       ├── specs/                     # Design specifications
│       └── plans/                     # Per-milestone implementation plans
└── mabipacade.sln
```

**Tech stack:** .NET 10 · C# 13 · SharpPcap 6.3.1 · PacketDotNet 1.4.8 · System.CommandLine 2.0.0-beta5 · Fleck 1.2 · WPF · xUnit

---

## Pipeline Stages

```
┌──────────────────────────────────────────────────────────────────────┐
│  Stage 0  Source        IFrameSource (live / pcap share abstraction) │
│  Stage 1  L2/L3/L4      PacketDotNet → TcpFrame                      │
│  Stage 2  Direction     BPF `src host` filter — server → client only │
│  Stage 3  Reassembly    Per-5-tuple TcpReassembler (seq order + dup) │
│  Stage 4  Framing       MabiPacketFramer (sign+length+flag+op+id+body)│
│  Stage 5  L2 decode     MessageElemReader (uvarint count, BE elems)  │
│  Stage 6  L3 plugin     DecoderRegistry.TryGet(op) → IPacketDecoder  │
│  Stage 7  Fan-out       PacketReceived event + PcapWriter + sinks    │
└──────────────────────────────────────────────────────────────────────┘
```

Failure modes:
- Stage 4 `FramingError` → reassembler `Reset()`, emit `SessionEvent.FrameResync`
- Stage 5 `BadBody` (tag outside 1..7, truncated length, count overflow) → skip the whole packet, emit `SessionEvent.BadBody`
- Stage 6 decoder throws → fall back to L2 emit, emit `SessionEvent.DecoderFailed`
- Stage 7 sink too slow → drop oldest + emit `SessionEvent.SinkOverflow` (never blocks pipeline)

---

## Wire format quick reference

Outer header (6 bytes):

| Offset | Size | Field | Encoding |
|---|---|---|---|
| 0 | 1 | sign | byte (unused) |
| 1 | 4 | length | uint32 **LE** (includes the 6-byte header) |
| 5 | 1 | flag | byte (0/3/4 = normal, 1/2 = short heartbeat, >4 = framing error) |

Body of a normal packet (`body = bytes[6..length]`):

| Offset | Size | Field | Encoding |
|---|---|---|---|
| 0 | 4 | op | uint32 **BE** (top 16 bits = 0, cast to ushort) |
| 4 | 8 | entityId | uint64 **BE** |
| 12 | varies | reserved uvarint + Message body | |

Message body: `[outer reserved uvarint][count uvarint][reserved 0 byte][elem * N]`

Elem: `[tag:1][value]`, tag ∈ 1..7:

| Tag | Type | Value |
|---|---|---|
| 1 | Byte | 1 byte |
| 2 | Short | uint16 **BE** |
| 3 | Int | uint32 **BE** |
| 4 | Long | uint64 **BE** |
| 5 | Float | float32 **LE** (the only LE field) |
| 6 | String | uint16 **BE** length (includes trailing NUL) + UTF-8 bytes |
| 7 | Bin | uint16 **BE** length + raw bytes |

---

## Build / Test

```powershell
dotnet build -p:TreatWarningsAsErrors=true
dotnet test
```

Expected: **201 pass + 4 skip** (the skips are fixture/environment-dependent tests).

To run the E2E pcap test, drop a pcap at:
```
tests/Mabipacade.Core.Tests/fixtures/known_good.pcap
```
(gitignored), then remove the `[Fact(Skip=...)]` attribute.

---

## Limitations / Not-yet

- **Korean server** packet encryption / iptime router relay setup — not supported
- **Outbound** direction (client → server) — `Direction.Outbound` enum value and NDJSON `dir` are reserved, not implemented
- **NIC roaming** (VPN / virtual switch) — `GameEndpointResolver` doesn't auto-switch, restart required
- **`--rate` / `--start-at` / `--end-at`** flags — Cli doesn't wire `ReplayTransport`; only the GUI has them
- **`--diagnostics summary`** — parsed but not implemented (falls back to `on`), deferred from M3
- **Most marker decoder body shapes** — unverified, so seeing a packet with an empty `decoded` POCO is expected
- **High `--rate` + heavy packet volume** in the GUI — `ObservableCollection` adds one packet at a time without batching, can stutter (improvement targeted for M3.5+)

---

## Design docs

- **Design spec** — [`docs/superpowers/specs/2026-05-13-mabipacade-design.md`](docs/superpowers/specs/2026-05-13-mabipacade-design.md)
- **Implementation plans** — `docs/superpowers/plans/` (M1 Core+Decoders, M2 Cli, M3 DebugUi)
- **Reference notes** — `D:/Projects/Notes/mabinogi-packet-decoding/README.md` (wire format, opcode table, traps the author hit)

---

## License

MIT — see [LICENSE](LICENSE).
