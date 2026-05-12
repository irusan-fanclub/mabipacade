# Mabipacade M1: Core + Decoders Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `Mabipacade.Core` (parser library + capture + recording + replay) and `Mabipacade.Decoders` (24 L3 decoders) so that .NET consumers can subscribe to `MabiPacket` events.

**Architecture:** Two-class-library solution. `Mabipacade.Core` exposes a `PacketPipeline` facade backed by a 7-stage pipeline (frame source → TCP reassembly → game packet framing → L2 elem decode → L3 plugin → fan-out). `Mabipacade.Decoders` registers 24 strongly-typed POCO decoders against `Core`'s plugin point. Live capture uses SharpPcap + PacketDotNet to a Windows NIC; replay reads pcap files via the same `IFrameSource` interface. Reconnect is handled by polling the Win32 TCP table and re-resolving the game endpoint.

**Tech Stack:** .NET 10 · C# 13 · xUnit · SharpPcap 6.3.1 · PacketDotNet 1.4.8 · `System.Threading.Channels` for fan-out.

**Reference material:**
- Design spec: `docs/superpowers/specs/2026-05-13-mabipacade-design.md`
- Wire format / op table: `D:/Projects/Notes/mabinogi-packet-decoding/README.md`
- Existing reference impl (re-implement, do not copy): `D:/Projects/mabi_stage4_boss_notifier/MabiStage4Notifier.Core/Packet/`
- Pcap fixtures: `D:/Projects/mabi_stage4_boss_notifier/publish/logs/*.pcap`

---

## Wire format (verified against reference impl)

These are the byte-level details every pipeline task depends on. **Read this once** before starting Phase 2.

**Outer game-packet header (6 bytes):**

| Offset | Size | Field | Encoding |
|---|---|---|---|
| 0 | 1 | sign | byte (unused by decoder, preserved for log) |
| 1 | 4 | length | uint32 **LE** (total length including these 6 bytes) |
| 5 | 1 | flag | byte |

**flag values:**
- `0`, `3`, `4` → normal packet (continue parsing body)
- `1`, `2` → short packet (heartbeat / keepalive); consume `length` bytes, emit no `MabiPacket`
- `> 4` → framing error

**Body of a normal packet (`body = bytes[6..length]`):**

| Offset in body | Size | Field | Encoding |
|---|---|---|---|
| 0 | 4 | op | uint32 **BIG-ENDIAN** (top 16 bits known to be 0 for current ops, cast to `ushort`) |
| 4 | 8 | entityId | uint64 **BIG-ENDIAN** |
| 12 | varies | outer-uvarint + Message | see below |

**Message format (starts at `body[12]`):**

```
[outer reserved uvarint]    ← skip; value typically 0
[count uvarint]              ← number of elems
[reserved byte = 0]
[elem 1][elem 2]…[elem N]
```

**Elem format:**

```
[1-byte tag][value bytes]
```

| Tag | Type | Value layout |
|---|---|---|
| 1 | Byte | 1 byte |
| 2 | Short | uint16 **BE** |
| 3 | Int | uint32 **BE** |
| 4 | Long | uint64 **BE** |
| 5 | Float | float32 **LE** (yes, float is the only LE field) |
| 6 | String | uint16 **BE** length-with-NUL, then UTF-8 bytes (visible string = `bytes[0..length-1]`) |
| 7 | Bin | uint16 **BE** length, then raw bytes |

Tag values 0 or > 7 → BadBody (skip whole packet).

**Uvarint (variable-length unsigned int):** standard 7-bit-per-byte little-endian encoding. Read bytes while high bit set; payload = low 7 bits shifted by 7×N. Max practical length for our use: 9 bytes.

**Important traps (from notes):**
- 4-byte payload right after a TCP 5-tuple change is the key-exchange handshake — skip it before parsing.
- `length` sanity cap: `0x100_0000` (16 MiB). Anything larger = framing error.
- Length includes the 6-byte header (so minimum normal packet = 6 + 12 + 1 = 19 bytes).

---

## File Structure

```
mabipacade/
├── mabipacade.sln
├── src/
│   ├── Mabipacade.Core/
│   │   ├── Mabipacade.Core.csproj
│   │   ├── Model/
│   │   │   ├── Direction.cs            (public enum)
│   │   │   ├── MessageElemType.cs      (public enum, tags 1..7)
│   │   │   ├── MessageElem.cs          (public readonly struct)
│   │   │   ├── MabiPacket.cs           (public sealed record)
│   │   │   ├── RawFrame.cs             (internal)
│   │   │   ├── TcpFrame.cs             (internal)
│   │   │   └── MabiPacketSlice.cs      (internal)
│   │   ├── Time/
│   │   │   └── Timestamp.cs            (UTC + ISO 8601 helpers)
│   │   ├── Plugins/
│   │   │   ├── DecoderInput.cs         (public readonly record struct)
│   │   │   └── IPacketDecoder.cs       (public interface)
│   │   ├── Pipeline/
│   │   │   ├── MessageElemReader.cs    (Stage 5)
│   │   │   ├── MabiPacketFramer.cs     (Stage 4)
│   │   │   ├── TcpReassembler.cs       (Stage 3)
│   │   │   ├── DecoderRegistry.cs      (Stage 6 plugin map)
│   │   │   └── PacketPipeline.cs       (facade — Stage 0..7)
│   │   ├── Sources/
│   │   │   ├── IFrameSource.cs
│   │   │   ├── PcapFileFrameSource.cs
│   │   │   └── LiveFrameSource.cs
│   │   ├── Capture/
│   │   │   ├── ProcessFinder.cs
│   │   │   ├── ITcpConnectionTable.cs
│   │   │   ├── Win32TcpConnectionTable.cs
│   │   │   ├── INicSelector.cs
│   │   │   ├── Win32NicSelector.cs
│   │   │   ├── RegionProfile.cs        (data + RegionProfiles.Taiwan)
│   │   │   └── GameEndpointResolver.cs
│   │   ├── Recording/
│   │   │   └── PcapWriter.cs
│   │   ├── Replay/
│   │   │   └── ReplayTransport.cs
│   │   └── Diagnostics/
│   │       ├── SessionEvent.cs
│   │       └── PipelineMetrics.cs
│   └── Mabipacade.Decoders/
│       ├── Mabipacade.Decoders.csproj
│       ├── OpCodes.cs                  (public enum, hex literals)
│       ├── DefaultDecoders.cs          (one-call register helper)
│       ├── Combat/
│       │   ├── CombatActionDecoder.cs           + POCO  (0x7924)
│       │   ├── CombatActionEndDecoder.cs        + POCO  (0x7925)
│       │   └── CombatActionPackDecoder.cs       + POCO  (0x7926)
│       ├── Skills/
│       │   ├── PlayerSkillPrepareStartDecoder.cs    + POCO  (0x6984)
│       │   ├── PlayerSkillPrepareProgressDecoder.cs + POCO  (0x6993)
│       │   ├── PlayerSkillPrepareReadyDecoder.cs    + POCO  (0x6985)
│       │   ├── PlayerSkillPostCastAck1Decoder.cs    + POCO  (0x6988)
│       │   ├── PlayerSkillPostCastAck2Decoder.cs    + POCO  (0x6989)
│       │   └── PlayerSkillStopDecoder.cs            + POCO  (0x698B)
│       ├── Entity/
│       │   ├── EntityAppearDecoder.cs           + POCO  (0x520C)
│       │   ├── EntityDisappearDecoder.cs        + POCO  (0x520D)
│       │   ├── EntitiesAppearDecoder.cs         + POCO  (0x5334)
│       │   ├── EntitiesDisappearDecoder.cs      + POCO  (0x5335)
│       │   └── IsNowDeadDecoder.cs              + POCO  (0x53FC)
│       ├── Stats/
│       │   ├── StatUpdatePrivateDecoder.cs      + POCO  (0x7530)
│       │   ├── StatUpdatePublicDecoder.cs       + POCO  (0x7532)
│       │   ├── EntityRelatedDecoder.cs          + POCO  (0x7534)
│       │   └── ConditionUpdate2Decoder.cs       + POCO  (0xA028)
│       └── Misc/
│           ├── ChatDecoder.cs                   + POCO  (0x526C)
│           ├── EffectDecoder.cs                 + POCO  (0x9091)
│           ├── EffectDelayedDecoder.cs          + POCO  (0x9095)
│           ├── SharpMindDecoder.cs              + POCO  (0xA41E)
│           ├── PartyWindowUpdateDecoder.cs      + POCO  (0xA43C)
│           └── EquipmentChangedDecoder.cs       + POCO  (0x59E6)
└── tests/
    ├── Mabipacade.Core.Tests/
    │   ├── Mabipacade.Core.Tests.csproj
    │   ├── Model/                              (record + elem tests)
    │   ├── Pipeline/                           (per-stage unit tests)
    │   ├── Sources/                            (PcapFileFrameSource)
    │   ├── Capture/                            (resolver with mock TCP table)
    │   ├── Recording/                          (PcapWriter)
    │   ├── Replay/                             (ReplayTransport)
    │   ├── EndToEnd/                           (full pipeline + pcap fixture)
    │   └── fixtures/
    │       ├── README.md
    │       └── *.pcap                          (copied from boss notifier; gitignored)
    └── Mabipacade.Decoders.Tests/
        ├── Mabipacade.Decoders.Tests.csproj
        └── (one test file per decoder)
```

**File ownership rule:** one decoder per file; POCO record + decoder class live in the same file (the file is the contract unit). All `Stage N` types under `Pipeline/` are pure functions (no I/O), unit-tested with hand-crafted bytes. All Win32-touching types live behind interfaces (`ITcpConnectionTable`, `INicSelector`) so `GameEndpointResolver` is testable with fakes.

---

## Conventions for every task

- **Run tests** with `dotnet test tests/<project>` from solution root. Engineer should always confirm the test fails before implementing (red), then passes after (green).
- **Commit messages:** `feat:`, `test:`, `chore:`, `refactor:` prefixes; subject ≤ 70 chars; co-author trailer with `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`.
- **No new dependencies** beyond what's in csproj specs unless a task explicitly adds one.
- **All public types**: `public sealed` records, `public sealed` classes; internal types: `internal sealed`. Avoid inheritance.
- **No comments** except where notes flag a known trap (e.g., `0x6993` `SkillId` index, 4-byte key exchange skip).

---

## Phase 0 — Solution skeleton

### Task 1: Create solution and Core project skeletons

**Files:**
- Create: `mabipacade.sln`
- Create: `src/Mabipacade.Core/Mabipacade.Core.csproj`
- Create: `src/Mabipacade.Decoders/Mabipacade.Decoders.csproj`
- Create: `tests/Mabipacade.Core.Tests/Mabipacade.Core.Tests.csproj`
- Create: `tests/Mabipacade.Decoders.Tests/Mabipacade.Decoders.Tests.csproj`

- [ ] **Step 1: Create solution file**

Run:
```
dotnet new sln -n mabipacade
```
Expected: `mabipacade.sln` created in repo root.

- [ ] **Step 2: Create Mabipacade.Core class library**

Run:
```
dotnet new classlib -n Mabipacade.Core -o src/Mabipacade.Core -f net10.0
dotnet sln add src/Mabipacade.Core/Mabipacade.Core.csproj
```
Delete the auto-generated `Class1.cs`.

Replace `src/Mabipacade.Core/Mabipacade.Core.csproj` contents with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="PacketDotNet" Version="1.4.8" />
    <PackageReference Include="SharpPcap" Version="6.3.1" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Mabipacade.Core.Tests" />
    <InternalsVisibleTo Include="Mabipacade.Decoders" />
    <InternalsVisibleTo Include="Mabipacade.Decoders.Tests" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create Mabipacade.Decoders class library**

Run:
```
dotnet new classlib -n Mabipacade.Decoders -o src/Mabipacade.Decoders -f net10.0
dotnet sln add src/Mabipacade.Decoders/Mabipacade.Decoders.csproj
```
Delete `Class1.cs`.

Replace `src/Mabipacade.Decoders/Mabipacade.Decoders.csproj` contents with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Mabipacade.Core\Mabipacade.Core.csproj" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Mabipacade.Decoders.Tests" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Create test projects with xUnit**

Run:
```
dotnet new xunit -n Mabipacade.Core.Tests -o tests/Mabipacade.Core.Tests -f net10.0
dotnet sln add tests/Mabipacade.Core.Tests/Mabipacade.Core.Tests.csproj
dotnet add tests/Mabipacade.Core.Tests reference src/Mabipacade.Core

dotnet new xunit -n Mabipacade.Decoders.Tests -o tests/Mabipacade.Decoders.Tests -f net10.0
dotnet sln add tests/Mabipacade.Decoders.Tests/Mabipacade.Decoders.Tests.csproj
dotnet add tests/Mabipacade.Decoders.Tests reference src/Mabipacade.Core
dotnet add tests/Mabipacade.Decoders.Tests reference src/Mabipacade.Decoders
```
Delete `UnitTest1.cs` from both test projects.

- [ ] **Step 5: Verify build**

Run:
```
dotnet build
```
Expected: solution builds with 0 errors, 0 warnings.

- [ ] **Step 6: Commit**

```
git add mabipacade.sln src/ tests/
git commit -m "$(cat <<'EOF'
chore: scaffold solution with Core + Decoders + tests

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 1 — Model layer

### Task 2: Direction enum, MessageElemType, MessageElem (TDD)

**Files:**
- Create: `src/Mabipacade.Core/Model/Direction.cs`
- Create: `src/Mabipacade.Core/Model/MessageElemType.cs`
- Create: `src/Mabipacade.Core/Model/MessageElem.cs`
- Create: `tests/Mabipacade.Core.Tests/Model/MessageElemTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/Mabipacade.Core.Tests/Model/MessageElemTests.cs`:
```csharp
using Mabipacade.Core.Model;

namespace Mabipacade.Core.Tests.Model;

public class MessageElemTests
{
    [Fact]
    public void Byte_ReturnsTagAndValue()
    {
        var e = MessageElem.Byte(42);
        Assert.Equal(MessageElemType.Byte, e.Type);
        Assert.Equal((byte)42, e.AsByte());
    }

    [Fact]
    public void Short_ReturnsTagAndValue()
    {
        var e = MessageElem.Short(59000);
        Assert.Equal(MessageElemType.Short, e.Type);
        Assert.Equal((ushort)59000, e.AsUInt16());
    }

    [Fact]
    public void String_ReturnsTagAndValue()
    {
        var e = MessageElem.String("hello");
        Assert.Equal(MessageElemType.String, e.Type);
        Assert.Equal("hello", e.AsString());
    }

    [Fact]
    public void AsString_Throws_WhenTypeIsByte()
    {
        var e = MessageElem.Byte(1);
        Assert.Throws<InvalidOperationException>(() => e.AsString());
    }
}
```

- [ ] **Step 2: Run test, confirm it fails to compile**

Run:
```
dotnet test tests/Mabipacade.Core.Tests
```
Expected: compile error (`MessageElem` / `MessageElemType` not found).

- [ ] **Step 3: Implement `Direction`**

`src/Mabipacade.Core/Model/Direction.cs`:
```csharp
namespace Mabipacade.Core.Model;

public enum Direction
{
    Inbound,
    Outbound
}
```

- [ ] **Step 4: Implement `MessageElemType`**

`src/Mabipacade.Core/Model/MessageElemType.cs`:
```csharp
namespace Mabipacade.Core.Model;

public enum MessageElemType : byte
{
    Byte = 1,
    Short = 2,
    Int = 3,
    Long = 4,
    Float = 5,
    String = 6,
    Bin = 7
}
```

- [ ] **Step 5: Implement `MessageElem`**

`src/Mabipacade.Core/Model/MessageElem.cs`:
```csharp
namespace Mabipacade.Core.Model;

public readonly struct MessageElem
{
    public MessageElemType Type { get; }
    private readonly object _value;

    private MessageElem(MessageElemType type, object value) { Type = type; _value = value; }

    public static MessageElem Byte(byte v)       => new(MessageElemType.Byte, v);
    public static MessageElem Short(ushort v)    => new(MessageElemType.Short, v);
    public static MessageElem Int(uint v)        => new(MessageElemType.Int, v);
    public static MessageElem Long(ulong v)      => new(MessageElemType.Long, v);
    public static MessageElem Float(float v)     => new(MessageElemType.Float, v);
    public static MessageElem String(string v)   => new(MessageElemType.String, v);
    public static MessageElem Bin(byte[] v)      => new(MessageElemType.Bin, v);

    public byte AsByte()       => Type == MessageElemType.Byte   ? (byte)_value   : throw Mismatch(MessageElemType.Byte);
    public ushort AsUInt16()   => Type == MessageElemType.Short  ? (ushort)_value : throw Mismatch(MessageElemType.Short);
    public uint AsUInt32()     => Type == MessageElemType.Int    ? (uint)_value   : throw Mismatch(MessageElemType.Int);
    public ulong AsUInt64()    => Type == MessageElemType.Long   ? (ulong)_value  : throw Mismatch(MessageElemType.Long);
    public float AsFloat()     => Type == MessageElemType.Float  ? (float)_value  : throw Mismatch(MessageElemType.Float);
    public string AsString()   => Type == MessageElemType.String ? (string)_value : throw Mismatch(MessageElemType.String);
    public byte[] AsBytes()    => Type == MessageElemType.Bin    ? (byte[])_value : throw Mismatch(MessageElemType.Bin);

    private InvalidOperationException Mismatch(MessageElemType expected) =>
        new($"Elem is {Type}, not {expected}");
}
```

- [ ] **Step 6: Run tests, confirm pass**

Run:
```
dotnet test tests/Mabipacade.Core.Tests
```
Expected: 4 passed.

- [ ] **Step 7: Commit**

```
git add src/Mabipacade.Core/Model/ tests/Mabipacade.Core.Tests/Model/
git commit -m "feat: add MessageElem, MessageElemType, Direction"
```

---

### Task 3: MabiPacket record

**Files:**
- Create: `src/Mabipacade.Core/Model/MabiPacket.cs`
- Create: `tests/Mabipacade.Core.Tests/Model/MabiPacketTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/Mabipacade.Core.Tests/Model/MabiPacketTests.cs`:
```csharp
using Mabipacade.Core.Model;

namespace Mabipacade.Core.Tests.Model;

public class MabiPacketTests
{
    [Fact]
    public void Construct_PreservesAllFields()
    {
        var ts = new DateTime(2026, 5, 13, 8, 0, 0, DateTimeKind.Utc);
        var elems = new[] { MessageElem.Short(59000) };
        var packet = new MabiPacket(ts, Direction.Inbound, 0x6984, 12345UL, elems, Decoded: null);

        Assert.Equal(ts, packet.TimestampUtc);
        Assert.Equal(Direction.Inbound, packet.Direction);
        Assert.Equal((ushort)0x6984, packet.Op);
        Assert.Equal(12345UL, packet.EntityId);
        Assert.Single(packet.Elems);
        Assert.Null(packet.Decoded);
    }

    [Fact]
    public void Records_AreEqual_ByValue()
    {
        var ts = DateTime.UtcNow;
        var elems = new[] { MessageElem.Short(1) };
        var a = new MabiPacket(ts, Direction.Inbound, 0x1, 0UL, elems, null);
        var b = new MabiPacket(ts, Direction.Inbound, 0x1, 0UL, elems, null);
        Assert.Equal(a, b);
    }
}
```

- [ ] **Step 2: Run test, confirm fail**

Run:
```
dotnet test tests/Mabipacade.Core.Tests --filter "FullyQualifiedName~MabiPacketTests"
```
Expected: compile error.

- [ ] **Step 3: Implement `MabiPacket`**

`src/Mabipacade.Core/Model/MabiPacket.cs`:
```csharp
namespace Mabipacade.Core.Model;

public sealed record MabiPacket(
    DateTime TimestampUtc,
    Direction Direction,
    ushort Op,
    ulong EntityId,
    IReadOnlyList<MessageElem> Elems,
    object? Decoded);
```

- [ ] **Step 4: Run, confirm pass**

Expected: 2 passed.

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Core/Model/MabiPacket.cs tests/Mabipacade.Core.Tests/Model/MabiPacketTests.cs
git commit -m "feat: add MabiPacket record"
```

---

### Task 4: Internal pipeline types (RawFrame, TcpFrame, MabiPacketSlice)

**Files:**
- Create: `src/Mabipacade.Core/Model/RawFrame.cs`
- Create: `src/Mabipacade.Core/Model/TcpFrame.cs`
- Create: `src/Mabipacade.Core/Model/MabiPacketSlice.cs`

These are internal — pipeline-only. No direct test; covered by stage tests later. They must compile.

- [ ] **Step 1: Create the three records**

`src/Mabipacade.Core/Model/RawFrame.cs`:
```csharp
using PacketDotNet;

namespace Mabipacade.Core.Model;

internal sealed record RawFrame(
    byte[] Data,
    LinkLayers LinkLayer,
    DateTime TimestampUtc);
```

`src/Mabipacade.Core/Model/TcpFrame.cs`:
```csharp
using System.Net;

namespace Mabipacade.Core.Model;

internal sealed record TcpFrame(
    IPAddress SrcIp,
    ushort SrcPort,
    IPAddress DstIp,
    ushort DstPort,
    uint SequenceNumber,
    byte[] Payload,
    DateTime TimestampUtc);
```

`src/Mabipacade.Core/Model/MabiPacketSlice.cs`:
```csharp
namespace Mabipacade.Core.Model;

internal sealed record MabiPacketSlice(
    ushort Op,
    ulong EntityId,
    byte[] Body,
    DateTime TimestampUtc);
```

- [ ] **Step 2: Verify build**

Run:
```
dotnet build
```
Expected: 0 errors, 0 warnings.

- [ ] **Step 3: Commit**

```
git add src/Mabipacade.Core/Model/RawFrame.cs src/Mabipacade.Core/Model/TcpFrame.cs src/Mabipacade.Core/Model/MabiPacketSlice.cs
git commit -m "feat: add internal pipeline types RawFrame, TcpFrame, MabiPacketSlice"
```

---

### Task 5: Timestamp helper

**Files:**
- Create: `src/Mabipacade.Core/Time/Timestamp.cs`
- Create: `tests/Mabipacade.Core.Tests/Time/TimestampTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Mabipacade.Core.Time;

namespace Mabipacade.Core.Tests.Time;

public class TimestampTests
{
    [Fact]
    public void ToIso8601_UsesUtcWithMillis()
    {
        var t = new DateTime(2026, 5, 13, 8, 23, 11, 842, DateTimeKind.Utc);
        Assert.Equal("2026-05-13T08:23:11.842Z", Timestamp.ToIso8601(t));
    }

    [Fact]
    public void FromPcapTimeval_ReturnsUtc()
    {
        // 2026-05-13T08:23:11.842Z = 1778060591.842 unix seconds
        var dt = Timestamp.FromUnixSeconds(1778060591L, 842_000);
        Assert.Equal(DateTimeKind.Utc, dt.Kind);
        Assert.Equal(2026, dt.Year);
        Assert.Equal(842, dt.Millisecond);
    }

    [Fact]
    public void ToIso8601_RejectsLocalTime()
    {
        var local = DateTime.SpecifyKind(new DateTime(2026, 5, 13), DateTimeKind.Local);
        Assert.Throws<ArgumentException>(() => Timestamp.ToIso8601(local));
    }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement**

`src/Mabipacade.Core/Time/Timestamp.cs`:
```csharp
namespace Mabipacade.Core.Time;

public static class Timestamp
{
    public static string ToIso8601(DateTime t)
    {
        if (t.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timestamp must be UTC", nameof(t));
        return t.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    }

    public static DateTime FromUnixSeconds(long seconds, int microseconds)
    {
        return DateTime.UnixEpoch
            .AddSeconds(seconds)
            .AddTicks(microseconds * 10);
    }
}
```

- [ ] **Step 4: Run tests, confirm pass**

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Core/Time/ tests/Mabipacade.Core.Tests/Time/
git commit -m "feat: add Timestamp helper enforcing UTC ISO 8601"
```

---

### Task 6: DecoderInput + IPacketDecoder

**Files:**
- Create: `src/Mabipacade.Core/Plugins/DecoderInput.cs`
- Create: `src/Mabipacade.Core/Plugins/IPacketDecoder.cs`
- Create: `tests/Mabipacade.Core.Tests/Plugins/DecoderInputTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Core.Tests.Plugins;

public class DecoderInputTests
{
    [Fact]
    public void Construct_PreservesAllFields()
    {
        var ts = DateTime.UtcNow;
        var elems = new[] { MessageElem.Short(1) };
        var input = new DecoderInput(ts, Direction.Inbound, 0x520C, 1UL, elems);

        Assert.Equal(ts, input.TimestampUtc);
        Assert.Equal((ushort)0x520C, input.Op);
        Assert.Equal(1UL, input.EntityId);
        Assert.Same(elems, input.Elems);
    }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement**

`src/Mabipacade.Core/Plugins/DecoderInput.cs`:
```csharp
using Mabipacade.Core.Model;

namespace Mabipacade.Core.Plugins;

public readonly record struct DecoderInput(
    DateTime TimestampUtc,
    Direction Direction,
    ushort Op,
    ulong EntityId,
    IReadOnlyList<MessageElem> Elems);
```

`src/Mabipacade.Core/Plugins/IPacketDecoder.cs`:
```csharp
namespace Mabipacade.Core.Plugins;

public interface IPacketDecoder
{
    ushort Op { get; }
    object Decode(DecoderInput input);
}
```

- [ ] **Step 4: Run, confirm pass**

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Core/Plugins/ tests/Mabipacade.Core.Tests/Plugins/
git commit -m "feat: add IPacketDecoder plugin interface + DecoderInput"
```

---

## Phase 2 — Pipeline core stages

### Task 6.5: Uvarint helper

Variable-length unsigned int decoder. Used by `MessageElemReader` to read the message count + outer reserved field.

**Files:**
- Create: `src/Mabipacade.Core/Pipeline/Uvarint.cs`
- Create: `tests/Mabipacade.Core.Tests/Pipeline/UvarintTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Mabipacade.Core.Pipeline;

namespace Mabipacade.Core.Tests.Pipeline;

public class UvarintTests
{
    [Fact]
    public void Reads_SingleByte_LowValue()
    {
        var data = new byte[] { 0x05 };
        Assert.True(Uvarint.TryRead(data, out ulong value, out int consumed));
        Assert.Equal(5UL, value);
        Assert.Equal(1, consumed);
    }

    [Fact]
    public void Reads_TwoBytes_BoundaryValue()
    {
        // 128 = 0x80 = 0b10000000 → uvarint: 0x80 0x01
        var data = new byte[] { 0x80, 0x01 };
        Assert.True(Uvarint.TryRead(data, out ulong value, out int consumed));
        Assert.Equal(128UL, value);
        Assert.Equal(2, consumed);
    }

    [Fact]
    public void Reads_Zero()
    {
        Assert.True(Uvarint.TryRead(new byte[] { 0x00 }, out ulong value, out int consumed));
        Assert.Equal(0UL, value);
        Assert.Equal(1, consumed);
    }

    [Fact]
    public void Returns_False_OnTruncated()
    {
        // 0x80 alone = high bit set, no continuation byte
        var data = new byte[] { 0x80 };
        Assert.False(Uvarint.TryRead(data, out _, out _));
    }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement**

`src/Mabipacade.Core/Pipeline/Uvarint.cs`:
```csharp
namespace Mabipacade.Core.Pipeline;

internal static class Uvarint
{
    public static bool TryRead(ReadOnlySpan<byte> data, out ulong value, out int consumed)
    {
        value = 0;
        consumed = 0;
        int shift = 0;
        for (int i = 0; i < data.Length && i < 10; i++)
        {
            byte b = data[i];
            value |= ((ulong)(b & 0x7F)) << shift;
            consumed = i + 1;
            if ((b & 0x80) == 0) return true;
            shift += 7;
        }
        consumed = 0;
        value = 0;
        return false;
    }
}
```

- [ ] **Step 4: Run, confirm pass**

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Core/Pipeline/Uvarint.cs tests/Mabipacade.Core.Tests/Pipeline/UvarintTests.cs
git commit -m "feat(pipeline): add Uvarint helper"
```

---

### Task 7: MessageElemReader (Stage 5)

Decodes a Message body byte buffer into a list of `MessageElem`. **See top-of-plan "Wire format" section for byte-level details.**

Input is the bytes **after** the outer 12-byte op+entityId header (i.e., `body[12..]` from the framer). Layout:

```
[outer reserved uvarint]   ← skip
[count uvarint]            ← number of elems
[reserved byte = 0]
[elem 1][elem 2]…[elem N]
```

Each elem = `[1-byte tag][value]`. Byte order: Short/Int/Long/String-length/Bin-length are **BIG-endian**; Float is the only LE field. String length includes a trailing NUL byte (visible string = `bytes[0..length-1]`).

Tags outside 1..7 → return `BadBody` (signal to caller to skip the whole packet).

**Files:**
- Create: `src/Mabipacade.Core/Pipeline/MessageElemReader.cs`
- Create: `tests/Mabipacade.Core.Tests/Pipeline/MessageElemReaderTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Mabipacade.Core.Model;
using Mabipacade.Core.Pipeline;

namespace Mabipacade.Core.Tests.Pipeline;

public class MessageElemReaderTests
{
    // Helpers to build wire-format byte sequences:
    // [outer uvarint=0][count uvarint][reserved 0 byte][elems...]
    private static byte[] Header(int count) => new byte[] { 0x00, (byte)count, 0x00 };

    [Fact]
    public void Reads_SingleShort_BigEndian()
    {
        // tag=2 (Short), value=59000 (0xE678 BE = 0xE6 0x78)
        var body = Header(1).Concat(new byte[] { 0x02, 0xE6, 0x78 }).ToArray();
        var result = MessageElemReader.TryRead(body, out var elems);
        Assert.Equal(ReadElemsResult.Ok, result);
        Assert.Single(elems);
        Assert.Equal(MessageElemType.Short, elems[0].Type);
        Assert.Equal((ushort)59000, elems[0].AsUInt16());
    }

    [Fact]
    public void Reads_ByteShortIntLong_BigEndian()
    {
        var body = Header(4).Concat(new byte[]
        {
            0x01, 0x2A,                               // Byte(42)
            0x02, 0x00, 0x01,                         // Short(1) BE
            0x03, 0x01, 0x02, 0x03, 0x04,             // Int(0x01020304) BE
            0x04, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08  // Long(0x0102030405060708) BE
        }).ToArray();
        var result = MessageElemReader.TryRead(body, out var elems);
        Assert.Equal(ReadElemsResult.Ok, result);
        Assert.Equal(4, elems.Count);
        Assert.Equal((byte)42, elems[0].AsByte());
        Assert.Equal((ushort)1, elems[1].AsUInt16());
        Assert.Equal(0x01020304u, elems[2].AsUInt32());
        Assert.Equal(0x0102030405060708UL, elems[3].AsUInt64());
    }

    [Fact]
    public void Reads_String_BeLength_StripsTrailingNul()
    {
        // tag=6, length=6 (BE: 0x00 0x06; "hello\0" = 6 bytes; visible 5)
        var body = Header(1).Concat(new byte[]
        {
            0x06, 0x00, 0x06, 0x68, 0x65, 0x6C, 0x6C, 0x6F, 0x00
        }).ToArray();
        var result = MessageElemReader.TryRead(body, out var elems);
        Assert.Equal(ReadElemsResult.Ok, result);
        Assert.Equal("hello", elems[0].AsString());
    }

    [Fact]
    public void Reads_Float_LittleEndian()
    {
        // tag=5, 1.0f = 0x3F800000; in LE wire = 0x00 0x00 0x80 0x3F
        var body = Header(1).Concat(new byte[] { 0x05, 0x00, 0x00, 0x80, 0x3F }).ToArray();
        var result = MessageElemReader.TryRead(body, out var elems);
        Assert.Equal(ReadElemsResult.Ok, result);
        Assert.Equal(1.0f, elems[0].AsFloat());
    }

    [Fact]
    public void Returns_BadBody_OnUnknownTag()
    {
        var body = Header(1).Concat(new byte[] { 0xAA, 0x00 }).ToArray();
        var result = MessageElemReader.TryRead(body, out var elems);
        Assert.Equal(ReadElemsResult.BadBody, result);
        Assert.Empty(elems);
    }

    [Fact]
    public void Returns_BadBody_OnTruncatedShort()
    {
        // tag=2 (Short) needs 2 bytes; only 1 supplied
        var body = Header(1).Concat(new byte[] { 0x02, 0x78 }).ToArray();
        var result = MessageElemReader.TryRead(body, out var elems);
        Assert.Equal(ReadElemsResult.BadBody, result);
    }

    [Fact]
    public void Returns_Ok_OnZeroCount()
    {
        var body = Header(0);
        var result = MessageElemReader.TryRead(body, out var elems);
        Assert.Equal(ReadElemsResult.Ok, result);
        Assert.Empty(elems);
    }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement**

`src/Mabipacade.Core/Pipeline/MessageElemReader.cs`:
```csharp
using System.Buffers.Binary;
using System.Text;
using Mabipacade.Core.Model;

namespace Mabipacade.Core.Pipeline;

public enum ReadElemsResult { Ok, BadBody }

public static class MessageElemReader
{
    public static ReadElemsResult TryRead(ReadOnlySpan<byte> body, out IReadOnlyList<MessageElem> elems)
    {
        elems = Array.Empty<MessageElem>();

        // Skip the outer reserved uvarint.
        if (!Uvarint.TryRead(body, out _, out int consumed)) return ReadElemsResult.BadBody;
        int offset = consumed;

        // Read the elem count uvarint.
        if (!Uvarint.TryRead(body.Slice(offset), out ulong count, out consumed)) return ReadElemsResult.BadBody;
        offset += consumed;

        // Skip the reserved zero byte.
        if (offset >= body.Length) return ReadElemsResult.BadBody;
        offset += 1;

        var list = new List<MessageElem>((int)count);
        for (ulong i = 0; i < count; i++)
        {
            if (offset >= body.Length) return ReadElemsResult.BadBody;
            byte tag = body[offset++];
            switch ((MessageElemType)tag)
            {
                case MessageElemType.Byte:
                    if (offset + 1 > body.Length) return ReadElemsResult.BadBody;
                    list.Add(MessageElem.Byte(body[offset]));
                    offset += 1;
                    break;
                case MessageElemType.Short:
                    if (offset + 2 > body.Length) return ReadElemsResult.BadBody;
                    list.Add(MessageElem.Short(BinaryPrimitives.ReadUInt16BigEndian(body.Slice(offset, 2))));
                    offset += 2;
                    break;
                case MessageElemType.Int:
                    if (offset + 4 > body.Length) return ReadElemsResult.BadBody;
                    list.Add(MessageElem.Int(BinaryPrimitives.ReadUInt32BigEndian(body.Slice(offset, 4))));
                    offset += 4;
                    break;
                case MessageElemType.Long:
                    if (offset + 8 > body.Length) return ReadElemsResult.BadBody;
                    list.Add(MessageElem.Long(BinaryPrimitives.ReadUInt64BigEndian(body.Slice(offset, 8))));
                    offset += 8;
                    break;
                case MessageElemType.Float:
                    if (offset + 4 > body.Length) return ReadElemsResult.BadBody;
                    list.Add(MessageElem.Float(BinaryPrimitives.ReadSingleLittleEndian(body.Slice(offset, 4))));
                    offset += 4;
                    break;
                case MessageElemType.String:
                {
                    if (offset + 2 > body.Length) return ReadElemsResult.BadBody;
                    ushort len = BinaryPrimitives.ReadUInt16BigEndian(body.Slice(offset, 2));
                    offset += 2;
                    if (offset + len > body.Length) return ReadElemsResult.BadBody;
                    int visible = len == 0 ? 0 : len - 1;        // strip trailing NUL
                    list.Add(MessageElem.String(Encoding.UTF8.GetString(body.Slice(offset, visible))));
                    offset += len;
                    break;
                }
                case MessageElemType.Bin:
                {
                    if (offset + 2 > body.Length) return ReadElemsResult.BadBody;
                    ushort len = BinaryPrimitives.ReadUInt16BigEndian(body.Slice(offset, 2));
                    offset += 2;
                    if (offset + len > body.Length) return ReadElemsResult.BadBody;
                    list.Add(MessageElem.Bin(body.Slice(offset, len).ToArray()));
                    offset += len;
                    break;
                }
                default:
                    return ReadElemsResult.BadBody;
            }
        }
        elems = list;
        return ReadElemsResult.Ok;
    }
}
```

- [ ] **Step 4: Run, confirm pass (6 tests)**

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Core/Pipeline/MessageElemReader.cs tests/Mabipacade.Core.Tests/Pipeline/MessageElemReaderTests.cs
git commit -m "feat(pipeline): add Stage 5 MessageElemReader with BadBody fault model"
```

---

### Task 8: MabiPacketFramer (Stage 4)

Reads a contiguous byte buffer and slices out individual Mabinogi game packets. **See top-of-plan "Wire format" section for byte-level layout.**

Summary:
- 6-byte outer header: `[sign:1][length:4 LE][flag:1]`
- flag ∈ {1, 2} → short heartbeat: consume `length` bytes, emit nothing, return `Ok` with `slice = null`
- flag ∈ {0, 3, 4} → normal packet: body starts at offset 6
- body[0..4] = op (BE uint32, cast to ushort)
- body[4..12] = entityId (BE uint64)
- body[12..length] = Message bytes (passed to `MessageElemReader`)
- length sanity cap: `0x100_0000`

Four return states:

| State | Meaning | Caller action |
|---|---|---|
| `Ok` (slice ≠ null) | Normal packet sliced | Advance buffer by `consumed`, loop |
| `Ok` (slice = null) | Short heartbeat consumed | Advance buffer by `consumed`, loop |
| `NeedMore` | Header says length=N but buffer < N | Wait for more data |
| `FramingError` | Length bogus or invalid flag | Drop oldest segment, realign |

**Files:**
- Create: `src/Mabipacade.Core/Pipeline/MabiPacketFramer.cs`
- Create: `tests/Mabipacade.Core.Tests/Pipeline/MabiPacketFramerTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using Mabipacade.Core.Model;
using Mabipacade.Core.Pipeline;

namespace Mabipacade.Core.Tests.Pipeline;

public class MabiPacketFramerTests
{
    [Fact]
    public void Ok_ParsesSingleMinimalPacket()
    {
        // Build a synthetic Mabinogi packet:
        //   sign=0x00, length=19 total, flag=0x00 (normal),
        //   op=0x6984 (BE in body), entityId=0x12345678AABBCCDD (BE),
        //   body[12..] = single 0x00 byte (outer reserved uvarint = 0)
        var bytes = TestPacketBuilder.BuildNormal(op: 0x6984, entityId: 0x12345678AABBCCDDUL,
                                                  bodyTail: new byte[] { 0x00 });
        var result = MabiPacketFramer.TryReadOne(bytes, out var slice, out int consumed);
        Assert.Equal(FrameResult.Ok, result);
        Assert.NotNull(slice);
        Assert.Equal((ushort)0x6984, slice!.Op);
        Assert.Equal(0x12345678AABBCCDDUL, slice.EntityId);
        Assert.Equal(bytes.Length, consumed);
    }

    [Fact]
    public void Ok_ShortPacket_FlagOne_ReturnsNullSlice()
    {
        // flag=1 → short heartbeat. Consume length bytes, emit no slice.
        var bytes = TestPacketBuilder.BuildShort(flag: 1, payloadLength: 8);
        var result = MabiPacketFramer.TryReadOne(bytes, out var slice, out int consumed);
        Assert.Equal(FrameResult.Ok, result);
        Assert.Null(slice);
        Assert.Equal(bytes.Length, consumed);
    }

    [Fact]
    public void NeedMore_WhenLengthExceedsBuffer()
    {
        var bytes = TestPacketBuilder.BuildNormal(op: 0x6984, entityId: 0UL,
                                                  bodyTail: new byte[80]);
        var truncated = bytes[..(bytes.Length / 2)];
        var result = MabiPacketFramer.TryReadOne(truncated, out _, out int consumed);
        Assert.Equal(FrameResult.NeedMore, result);
        Assert.Equal(0, consumed);
    }

    [Fact]
    public void FramingError_OnObsceneLength()
    {
        // sign=0, length=0xFFFFFFFF, flag=0
        var bytes = new byte[] { 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00 };
        var result = MabiPacketFramer.TryReadOne(bytes, out _, out _);
        Assert.Equal(FrameResult.FramingError, result);
    }

    [Fact]
    public void FramingError_OnInvalidFlag()
    {
        // flag=5 (only 0..4 are legal)
        var bytes = new byte[] { 0x00, 0x13, 0x00, 0x00, 0x00, 0x05 }; // length=0x13=19
        Array.Resize(ref bytes, 19);
        var result = MabiPacketFramer.TryReadOne(bytes, out _, out _);
        Assert.Equal(FrameResult.FramingError, result);
    }

    [Fact]
    public void Ok_ConsumesExactly_LeavesTrailing()
    {
        var p1 = TestPacketBuilder.BuildNormal(op: 0x6984, entityId: 0UL, bodyTail: new byte[] { 0x00 });
        var p2 = TestPacketBuilder.BuildNormal(op: 0x6985, entityId: 1UL, bodyTail: new byte[] { 0x00, 0x00, 0x00 });
        var combined = p1.Concat(p2).ToArray();

        Assert.Equal(FrameResult.Ok, MabiPacketFramer.TryReadOne(combined, out var first, out int c1));
        Assert.Equal((ushort)0x6984, first!.Op);
        Assert.Equal(p1.Length, c1);

        Assert.Equal(FrameResult.Ok, MabiPacketFramer.TryReadOne(combined.AsSpan(c1), out var second, out int c2));
        Assert.Equal((ushort)0x6985, second!.Op);
        Assert.Equal(p2.Length, c2);
    }
}
```

Create test helper `tests/Mabipacade.Core.Tests/Pipeline/TestPacketBuilder.cs`:

```csharp
using System.Buffers.Binary;

namespace Mabipacade.Core.Tests.Pipeline;

internal static class TestPacketBuilder
{
    // Layout: [sign:1][length:4 LE][flag:1][op:4 BE][entityId:8 BE][bodyTail...]
    public static byte[] BuildNormal(ushort op, ulong entityId, byte[] bodyTail)
    {
        int total = 6 + 4 + 8 + bodyTail.Length;
        var buf = new byte[total];
        buf[0] = 0x00;                                                        // sign
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(1, 4), (uint)total);
        buf[5] = 0x00;                                                        // flag = normal
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(6, 4), op);
        BinaryPrimitives.WriteUInt64BigEndian(buf.AsSpan(10, 8), entityId);
        bodyTail.CopyTo(buf.AsSpan(18));
        return buf;
    }

    public static byte[] BuildShort(byte flag, int payloadLength)
    {
        int total = 6 + payloadLength;
        var buf = new byte[total];
        buf[0] = 0x00;
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(1, 4), (uint)total);
        buf[5] = flag;
        return buf;
    }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement `MabiPacketFramer`**

`src/Mabipacade.Core/Pipeline/MabiPacketFramer.cs`:
```csharp
using System.Buffers.Binary;
using Mabipacade.Core.Model;

namespace Mabipacade.Core.Pipeline;

public enum FrameResult { Ok, NeedMore, FramingError }

public static class MabiPacketFramer
{
    private const int HeaderSize = 6;
    private const int BodyMinSize = 4 + 8 + 1;                  // op + entityId + outer uvarint
    private const uint MaxPacketLength = 0x100_0000;

    public static FrameResult TryReadOne(ReadOnlySpan<byte> buffer, out MabiPacketSlice? slice, out int consumed)
    {
        slice = null;
        consumed = 0;

        if (buffer.Length < HeaderSize) return FrameResult.NeedMore;

        uint length = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(1, 4));
        byte flag = buffer[5];

        if (length == 0 || length > MaxPacketLength) return FrameResult.FramingError;
        if (flag > 4) return FrameResult.FramingError;

        bool isShort = flag == 1 || flag == 2;
        if (isShort)
        {
            if (buffer.Length < length) return FrameResult.NeedMore;
            if (length < HeaderSize) return FrameResult.FramingError;
            consumed = (int)length;
            return FrameResult.Ok;                              // slice stays null
        }

        if (length < HeaderSize + BodyMinSize) return FrameResult.FramingError;
        if (buffer.Length < length) return FrameResult.NeedMore;

        var body = buffer.Slice(HeaderSize, (int)length - HeaderSize);
        uint op = BinaryPrimitives.ReadUInt32BigEndian(body.Slice(0, 4));
        ulong entityId = BinaryPrimitives.ReadUInt64BigEndian(body.Slice(4, 8));
        var msg = body.Slice(12).ToArray();

        slice = new MabiPacketSlice((ushort)op, entityId, msg, DateTime.UnixEpoch);
        consumed = (int)length;
        return FrameResult.Ok;
    }
}
```

**Engineer note:** The pipeline caller fills in the real pcap timestamp; we use `DateTime.UnixEpoch` here as a sentinel inside `MabiPacketSlice.TimestampUtc`. `op` is cast from uint32 to ushort because all known ops fit in 16 bits (notes confirm).

- [ ] **Step 5: Run tests, confirm pass**

- [ ] **Step 6: Commit**

```
git add src/Mabipacade.Core/Pipeline/MabiPacketFramer.cs tests/Mabipacade.Core.Tests/Pipeline/MabiPacketFramerTests.cs tests/Mabipacade.Core.Tests/Pipeline/TestPacketBuilder.cs
git commit -m "feat(pipeline): add Stage 4 MabiPacketFramer with Ok/NeedMore/FramingError states"
```

---

### Task 9: TcpReassembler (Stage 3)

Maintains a per-5-tuple byte buffer, orders TCP segments by sequence number, drops duplicates, and exposes a contiguous `ReadOnlySpan<byte>` for the framer to consume. On 5-tuple change (channel/server switch), flush the buffer and reset base sequence.

Behavior pulled from `PacketReader.cs` (existing impl); re-implement, do not copy.

**Files:**
- Create: `src/Mabipacade.Core/Pipeline/TcpReassembler.cs`
- Create: `tests/Mabipacade.Core.Tests/Pipeline/TcpReassemblerTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Net;
using Mabipacade.Core.Model;
using Mabipacade.Core.Pipeline;

namespace Mabipacade.Core.Tests.Pipeline;

public class TcpReassemblerTests
{
    private static readonly IPAddress Server = IPAddress.Parse("10.0.0.1");
    private static readonly IPAddress Client = IPAddress.Parse("10.0.0.2");

    private static TcpFrame Frame(uint seq, byte[] payload)
        => new(Server, 11000, Client, 50000, seq, payload, DateTime.UtcNow);

    [Fact]
    public void Accept_InOrder_ConcatsPayloads()
    {
        var r = new TcpReassembler();
        r.Feed(Frame(1000, new byte[] { 1, 2, 3 }));
        r.Feed(Frame(1003, new byte[] { 4, 5 }));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, r.GetBuffer().ToArray());
    }

    [Fact]
    public void Accept_OutOfOrder_ReordersBeforeEmit()
    {
        var r = new TcpReassembler();
        r.Feed(Frame(1003, new byte[] { 4, 5 }));    // arrives first, before its predecessor
        r.Feed(Frame(1000, new byte[] { 1, 2, 3 })); // fills the gap
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, r.GetBuffer().ToArray());
    }

    [Fact]
    public void Drops_Duplicate()
    {
        var r = new TcpReassembler();
        r.Feed(Frame(1000, new byte[] { 1, 2, 3 }));
        r.Feed(Frame(1000, new byte[] { 1, 2, 3 })); // duplicate
        Assert.Equal(new byte[] { 1, 2, 3 }, r.GetBuffer().ToArray());
    }

    [Fact]
    public void Consume_RemovesBytesFromHead()
    {
        var r = new TcpReassembler();
        r.Feed(Frame(1000, new byte[] { 1, 2, 3, 4, 5 }));
        r.Consume(3);
        Assert.Equal(new byte[] { 4, 5 }, r.GetBuffer().ToArray());
    }

    [Fact]
    public void Reset_ClearsBuffer()
    {
        var r = new TcpReassembler();
        r.Feed(Frame(1000, new byte[] { 1, 2, 3 }));
        r.Reset();
        Assert.True(r.GetBuffer().IsEmpty);
    }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement**

`src/Mabipacade.Core/Pipeline/TcpReassembler.cs`:
```csharp
using Mabipacade.Core.Model;

namespace Mabipacade.Core.Pipeline;

internal sealed class TcpReassembler
{
    private readonly List<byte> _buffer = new();
    private uint? _nextSeq;
    private readonly List<TcpFrame> _pending = new();

    public ReadOnlySpan<byte> GetBuffer() => _buffer.ToArray();

    public void Feed(TcpFrame frame)
    {
        if (frame.Payload.Length == 0) return;

        if (_nextSeq is null || frame.SequenceNumber == _nextSeq)
        {
            AppendInOrder(frame);
            DrainPending();
            return;
        }

        if (frame.SequenceNumber + frame.Payload.Length <= _nextSeq) return; // duplicate or stale
        _pending.Add(frame);
        _pending.Sort((a, b) => a.SequenceNumber.CompareTo(b.SequenceNumber));
        DrainPending();
    }

    public void Consume(int count)
    {
        if (count <= 0) return;
        if (count > _buffer.Count) count = _buffer.Count;
        _buffer.RemoveRange(0, count);
    }

    public void Reset()
    {
        _buffer.Clear();
        _pending.Clear();
        _nextSeq = null;
    }

    private void AppendInOrder(TcpFrame frame)
    {
        _buffer.AddRange(frame.Payload);
        _nextSeq = frame.SequenceNumber + (uint)frame.Payload.Length;
    }

    private void DrainPending()
    {
        bool progress;
        do
        {
            progress = false;
            for (int i = 0; i < _pending.Count; i++)
            {
                var f = _pending[i];
                if (f.SequenceNumber == _nextSeq)
                {
                    AppendInOrder(f);
                    _pending.RemoveAt(i);
                    progress = true;
                    break;
                }
            }
        } while (progress);
    }
}
```

- [ ] **Step 4: Run tests, confirm pass**

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Core/Pipeline/TcpReassembler.cs tests/Mabipacade.Core.Tests/Pipeline/TcpReassemblerTests.cs
git commit -m "feat(pipeline): add Stage 3 TcpReassembler with seq ordering + dup drop"
```

---

### Task 10: DecoderRegistry (Stage 6)

A typed map from `ushort op` to `IPacketDecoder`. Lookup is hot-path; use `Dictionary<ushort, IPacketDecoder>` keyed by `op`.

**Files:**
- Create: `src/Mabipacade.Core/Pipeline/DecoderRegistry.cs`
- Create: `tests/Mabipacade.Core.Tests/Pipeline/DecoderRegistryTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Mabipacade.Core.Model;
using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Core.Tests.Pipeline;

public class DecoderRegistryTests
{
    private sealed class FakeDecoder : IPacketDecoder
    {
        public ushort Op { get; }
        public FakeDecoder(ushort op) { Op = op; }
        public object Decode(DecoderInput input) => "decoded";
    }

    [Fact]
    public void Register_AndLookup()
    {
        var reg = new DecoderRegistry();
        reg.Register(new FakeDecoder(0x6984));
        Assert.True(reg.TryGet(0x6984, out var d));
        Assert.Equal((ushort)0x6984, d!.Op);
    }

    [Fact]
    public void TryGet_ReturnsFalse_WhenMissing()
    {
        var reg = new DecoderRegistry();
        Assert.False(reg.TryGet(0x6984, out _));
    }

    [Fact]
    public void Register_ReplacesPrevious()
    {
        var reg = new DecoderRegistry();
        reg.Register(new FakeDecoder(0x6984));
        var second = new FakeDecoder(0x6984);
        reg.Register(second);
        reg.TryGet(0x6984, out var d);
        Assert.Same(second, d);
    }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement**

`src/Mabipacade.Core/Pipeline/DecoderRegistry.cs`:
```csharp
using Mabipacade.Core.Plugins;

namespace Mabipacade.Core.Pipeline;

public sealed class DecoderRegistry
{
    private readonly Dictionary<ushort, IPacketDecoder> _map = new();

    public void Register(IPacketDecoder decoder) => _map[decoder.Op] = decoder;

    public bool TryGet(ushort op, out IPacketDecoder? decoder) => _map.TryGetValue(op, out decoder);

    public IReadOnlyCollection<ushort> RegisteredOps => _map.Keys;
}
```

- [ ] **Step 4: Run, confirm pass**

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Core/Pipeline/DecoderRegistry.cs tests/Mabipacade.Core.Tests/Pipeline/DecoderRegistryTests.cs
git commit -m "feat(pipeline): add DecoderRegistry for L3 plugin lookup"
```

---

## Phase 3 — Frame sources

### Task 11: IFrameSource interface + PcapFileFrameSource

`IFrameSource` is Stage 0's abstraction. Live and replay implement it identically; consumers don't care which one they're on.

**Files:**
- Create: `src/Mabipacade.Core/Sources/IFrameSource.cs`
- Create: `src/Mabipacade.Core/Sources/PcapFileFrameSource.cs`
- Create: `tests/Mabipacade.Core.Tests/Sources/PcapFileFrameSourceTests.cs`

- [ ] **Step 1: Define interface**

`src/Mabipacade.Core/Sources/IFrameSource.cs`:
```csharp
using Mabipacade.Core.Model;

namespace Mabipacade.Core.Sources;

public interface IFrameSource : IDisposable
{
    event EventHandler<RawFrameEventArgs>? FrameReceived;
    event EventHandler? EndOfStream;
    Task StartAsync(CancellationToken ct);
    Task StopAsync();
}

public sealed class RawFrameEventArgs(byte[] data, PacketDotNet.LinkLayers linkLayer, DateTime timestampUtc) : EventArgs
{
    public byte[] Data { get; } = data;
    public PacketDotNet.LinkLayers LinkLayer { get; } = linkLayer;
    public DateTime TimestampUtc { get; } = timestampUtc;
}
```

`RawFrameEventArgs` exposes the same fields as `RawFrame` but is `public` because the event is on a public interface. (Internal `RawFrame` is used by `PacketPipeline`'s downstream stages.)

- [ ] **Step 2: Write the failing PcapFileFrameSource test**

This test depends on a fixture pcap file. Create `tests/Mabipacade.Core.Tests/fixtures/` directory and `fixtures/README.md`:
```markdown
# Test fixtures

Copy a small pcap file from `D:/Projects/mabi_stage4_boss_notifier/publish/logs/` here
as `tiny.pcap` (1-second snippet preferred to keep test fast). The pcap is not
committed to git (see top-level `.gitignore`).
```

Add to repo-level `.gitignore`:
```
tests/Mabipacade.Core.Tests/fixtures/*.pcap
!tests/Mabipacade.Core.Tests/fixtures/README.md
```

`tests/Mabipacade.Core.Tests/Sources/PcapFileFrameSourceTests.cs`:
```csharp
using Mabipacade.Core.Sources;

namespace Mabipacade.Core.Tests.Sources;

public class PcapFileFrameSourceTests
{
    private const string FixturePath = "fixtures/tiny.pcap";

    [Fact(Skip = "Requires local fixture")]
    public async Task EmitsFrames_FromPcap()
    {
        if (!File.Exists(FixturePath)) return;

        int frameCount = 0;
        bool eosFired = false;
        using var src = new PcapFileFrameSource(FixturePath);
        src.FrameReceived += (_, _) => Interlocked.Increment(ref frameCount);
        src.EndOfStream += (_, _) => eosFired = true;

        await src.StartAsync(CancellationToken.None);
        await src.StopAsync();

        Assert.True(frameCount > 0);
        Assert.True(eosFired);
    }
}
```

(The `[Fact(Skip = …)]` lets CI pass without the binary fixture; engineer copies the pcap locally to validate.)

- [ ] **Step 3: Run, confirm test is discovered + skipped**

- [ ] **Step 4: Implement `PcapFileFrameSource`**

`src/Mabipacade.Core/Sources/PcapFileFrameSource.cs`:
```csharp
using SharpPcap;
using SharpPcap.LibPcap;

namespace Mabipacade.Core.Sources;

public sealed class PcapFileFrameSource : IFrameSource
{
    private readonly string _path;
    private CaptureFileReaderDevice? _reader;
    private CancellationTokenSource? _cts;
    private TaskCompletionSource? _completion;

    public PcapFileFrameSource(string path) { _path = path; }

    public event EventHandler<RawFrameEventArgs>? FrameReceived;
    public event EventHandler? EndOfStream;

    public Task StartAsync(CancellationToken ct)
    {
        _reader = new CaptureFileReaderDevice(_path);
        _reader.Open(new DeviceConfiguration());
        _reader.OnPacketArrival += OnPacket;
        _reader.OnCaptureStopped += (_, _) =>
        {
            EndOfStream?.Invoke(this, EventArgs.Empty);
            _completion?.TrySetResult();
        };

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _completion = new TaskCompletionSource();
        _ = Task.Run(() => _reader.Capture(), _cts.Token);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _reader?.StopCapture();
        return _completion?.Task ?? Task.CompletedTask;
    }

    private void OnPacket(object sender, PacketCapture e)
    {
        var raw = e.GetPacket();
        FrameReceived?.Invoke(this, new RawFrameEventArgs(
            raw.Data,
            (PacketDotNet.LinkLayers)raw.LinkLayerType,
            raw.Timeval.Date.ToUniversalTime()));
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _reader?.Close();
        _reader?.Dispose();
        _cts?.Dispose();
    }
}
```

- [ ] **Step 5: Build, manually run with fixture**

Engineer copies a pcap to `tests/Mabipacade.Core.Tests/fixtures/tiny.pcap`, removes `Skip`, runs:
```
dotnet test tests/Mabipacade.Core.Tests --filter "FullyQualifiedName~PcapFileFrameSourceTests"
```
Confirm > 0 frames emitted, EndOfStream fires. Re-add `Skip` before commit so CI passes without fixture.

- [ ] **Step 6: Commit**

```
git add src/Mabipacade.Core/Sources/ tests/Mabipacade.Core.Tests/Sources/ tests/Mabipacade.Core.Tests/fixtures/README.md .gitignore
git commit -m "feat(sources): add IFrameSource + PcapFileFrameSource"
```

---

### Task 12: LiveFrameSource

Wraps a `SharpPcap` `CaptureDevice` (selected by the resolver). No automated test — capture from a live NIC is environment-dependent. Manual smoke test only.

**Files:**
- Create: `src/Mabipacade.Core/Sources/LiveFrameSource.cs`

- [ ] **Step 1: Implement**

`src/Mabipacade.Core/Sources/LiveFrameSource.cs`:
```csharp
using SharpPcap;

namespace Mabipacade.Core.Sources;

public sealed class LiveFrameSource : IFrameSource
{
    private readonly ICaptureDevice _device;
    private readonly string _bpfFilter;

    public LiveFrameSource(ICaptureDevice device, string bpfFilter)
    {
        _device = device;
        _bpfFilter = bpfFilter;
    }

    public event EventHandler<RawFrameEventArgs>? FrameReceived;
    public event EventHandler? EndOfStream;

    public Task StartAsync(CancellationToken ct)
    {
        _device.Open(DeviceModes.Promiscuous, 1000);
        _device.Filter = _bpfFilter;
        _device.OnPacketArrival += OnPacket;
        _device.StartCapture();
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _device.StopCapture();
        EndOfStream?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private void OnPacket(object sender, PacketCapture e)
    {
        var raw = e.GetPacket();
        FrameReceived?.Invoke(this, new RawFrameEventArgs(
            raw.Data,
            (PacketDotNet.LinkLayers)raw.LinkLayerType,
            raw.Timeval.Date.ToUniversalTime()));
    }

    public void Dispose() => _device.Close();
}
```

- [ ] **Step 2: Build**

Run: `dotnet build`. Expected: clean.

- [ ] **Step 3: Commit**

```
git add src/Mabipacade.Core/Sources/LiveFrameSource.cs
git commit -m "feat(sources): add LiveFrameSource over SharpPcap CaptureDevice"
```

---

## Phase 4 — Capture / Resolver

### Task 13: ProcessFinder

Locates a Mabinogi process by name; returns its PID (or null if not running).

**Files:**
- Create: `src/Mabipacade.Core/Capture/ProcessFinder.cs`
- Create: `tests/Mabipacade.Core.Tests/Capture/ProcessFinderTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Mabipacade.Core.Capture;

namespace Mabipacade.Core.Tests.Capture;

public class ProcessFinderTests
{
    [Fact]
    public void Find_ReturnsNull_WhenProcessAbsent()
    {
        var finder = new ProcessFinder();
        Assert.Null(finder.Find("DefinitelyNotARealProcess_xyz"));
    }

    [Fact]
    public void Find_ReturnsPid_ForCurrentProcess()
    {
        var finder = new ProcessFinder();
        var name = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
        var pid = finder.Find(name);
        Assert.NotNull(pid);
    }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement**

`src/Mabipacade.Core/Capture/ProcessFinder.cs`:
```csharp
using System.Diagnostics;

namespace Mabipacade.Core.Capture;

public sealed class ProcessFinder
{
    public int? Find(string processName)
    {
        var procs = Process.GetProcessesByName(processName);
        try
        {
            return procs.Length > 0 ? procs[0].Id : null;
        }
        finally
        {
            foreach (var p in procs) p.Dispose();
        }
    }
}
```

- [ ] **Step 4: Run tests, confirm pass**

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Core/Capture/ProcessFinder.cs tests/Mabipacade.Core.Tests/Capture/ProcessFinderTests.cs
git commit -m "feat(capture): add ProcessFinder"
```

---

### Task 14: TcpConnectionTable

Win32 wrapper for `GetExtendedTcpTable` (returns rows of `(localAddr, localPort, remoteAddr, remotePort, state, owningPid)`). Hide behind interface for testability.

**Files:**
- Create: `src/Mabipacade.Core/Capture/ITcpConnectionTable.cs`
- Create: `src/Mabipacade.Core/Capture/Win32TcpConnectionTable.cs`
- Create: `src/Mabipacade.Core/Capture/TcpConnectionRow.cs`

- [ ] **Step 1: Define interface + row type**

`src/Mabipacade.Core/Capture/TcpConnectionRow.cs`:
```csharp
using System.Net;

namespace Mabipacade.Core.Capture;

public sealed record TcpConnectionRow(
    IPAddress LocalAddress,
    ushort LocalPort,
    IPAddress RemoteAddress,
    ushort RemotePort,
    TcpConnectionState State,
    int OwningPid);

public enum TcpConnectionState
{
    Unknown,
    Closed,
    Listen,
    SynSent,
    SynReceived,
    Established,
    FinWait1,
    FinWait2,
    CloseWait,
    Closing,
    LastAck,
    TimeWait,
    DeleteTcb
}
```

`src/Mabipacade.Core/Capture/ITcpConnectionTable.cs`:
```csharp
namespace Mabipacade.Core.Capture;

public interface ITcpConnectionTable
{
    IReadOnlyList<TcpConnectionRow> GetConnections();
}
```

- [ ] **Step 2: Implement Win32 version**

`src/Mabipacade.Core/Capture/Win32TcpConnectionTable.cs`:
```csharp
using System.Net;
using System.Runtime.InteropServices;

namespace Mabipacade.Core.Capture;

public sealed class Win32TcpConnectionTable : ITcpConnectionTable
{
    public IReadOnlyList<TcpConnectionRow> GetConnections()
    {
        const int AF_INET = 2;
        const int TCP_TABLE_OWNER_PID_ALL = 5;

        int bufSize = 0;
        _ = GetExtendedTcpTable(IntPtr.Zero, ref bufSize, false, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
        var ptr = Marshal.AllocHGlobal(bufSize);
        try
        {
            int rc = GetExtendedTcpTable(ptr, ref bufSize, false, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
            if (rc != 0) throw new InvalidOperationException($"GetExtendedTcpTable rc={rc}");

            int rowCount = Marshal.ReadInt32(ptr);
            var rows = new List<TcpConnectionRow>(rowCount);
            IntPtr rowPtr = IntPtr.Add(ptr, 4);
            int rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
            for (int i = 0; i < rowCount; i++)
            {
                var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr);
                rows.Add(new TcpConnectionRow(
                    new IPAddress(row.localAddr),
                    (ushort)IPAddress.NetworkToHostOrder((short)row.localPort),
                    new IPAddress(row.remoteAddr),
                    (ushort)IPAddress.NetworkToHostOrder((short)row.remotePort),
                    MapState(row.state),
                    row.owningPid));
                rowPtr = IntPtr.Add(rowPtr, rowSize);
            }
            return rows;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static TcpConnectionState MapState(uint s) => s switch
    {
        1 => TcpConnectionState.Closed,
        2 => TcpConnectionState.Listen,
        3 => TcpConnectionState.SynSent,
        4 => TcpConnectionState.SynReceived,
        5 => TcpConnectionState.Established,
        6 => TcpConnectionState.FinWait1,
        7 => TcpConnectionState.FinWait2,
        8 => TcpConnectionState.CloseWait,
        9 => TcpConnectionState.Closing,
        10 => TcpConnectionState.LastAck,
        11 => TcpConnectionState.TimeWait,
        12 => TcpConnectionState.DeleteTcb,
        _ => TcpConnectionState.Unknown
    };

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern int GetExtendedTcpTable(
        IntPtr pTcpTable, ref int dwOutBufLen, bool sort,
        int ipVersion, int tableClass, uint reserved);

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID
    {
        public uint state;
        public uint localAddr;
        public uint localPort;
        public uint remoteAddr;
        public uint remotePort;
        public int owningPid;
    }
}
```

- [ ] **Step 3: Smoke test manually**

In a scratch console, instantiate and call `GetConnections()`, verify > 0 rows return.

- [ ] **Step 4: Build**

Run: `dotnet build`. Clean.

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Core/Capture/TcpConnectionRow.cs src/Mabipacade.Core/Capture/ITcpConnectionTable.cs src/Mabipacade.Core/Capture/Win32TcpConnectionTable.cs
git commit -m "feat(capture): add TcpConnectionTable Win32 wrapper"
```

---

### Task 15: RegionProfile

Data type + at least one populated `Taiwan` profile. The actual IP ranges are TBD per the spec; populate with empty arrays as the fallback path for v1 — the resolver will simply not match anything in fallback mode until ranges are known.

**Files:**
- Create: `src/Mabipacade.Core/Capture/RegionProfile.cs`
- Create: `tests/Mabipacade.Core.Tests/Capture/RegionProfileTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Net;
using Mabipacade.Core.Capture;

namespace Mabipacade.Core.Tests.Capture;

public class RegionProfileTests
{
    [Fact]
    public void Contains_ReturnsTrue_WhenIpInRange()
    {
        var profile = new RegionProfile(
            "test",
            new[] { new IpRange(IPAddress.Parse("10.0.0.0"), IPAddress.Parse("10.0.0.255")) },
            new ushort[] { 11000 });
        Assert.True(profile.Contains(IPAddress.Parse("10.0.0.42"), 11000));
        Assert.False(profile.Contains(IPAddress.Parse("11.0.0.1"), 11000));
        Assert.False(profile.Contains(IPAddress.Parse("10.0.0.42"), 22222));
    }

    [Fact]
    public void Taiwan_IsAvailable()
    {
        Assert.NotNull(RegionProfiles.Taiwan);
        Assert.Equal("tw", RegionProfiles.Taiwan.Name);
    }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement**

`src/Mabipacade.Core/Capture/RegionProfile.cs`:
```csharp
using System.Net;

namespace Mabipacade.Core.Capture;

public sealed record IpRange(IPAddress Start, IPAddress End)
{
    public bool Contains(IPAddress addr)
    {
        long a = ToLong(addr);
        return a >= ToLong(Start) && a <= ToLong(End);
    }
    private static long ToLong(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return ((long)b[0] << 24) | ((long)b[1] << 16) | ((long)b[2] << 8) | b[3];
    }
}

public sealed record RegionProfile(
    string Name,
    IReadOnlyList<IpRange> ServerRanges,
    IReadOnlyList<ushort> KnownPorts)
{
    public bool Contains(IPAddress addr, ushort port)
    {
        if (KnownPorts.Count > 0 && !KnownPorts.Contains(port)) return false;
        foreach (var r in ServerRanges)
            if (r.Contains(addr)) return true;
        return ServerRanges.Count == 0 ? false : false;
    }
}

public static class RegionProfiles
{
    public static RegionProfile Taiwan { get; } = new("tw",
        Array.Empty<IpRange>(),       // TODO populate when known
        new ushort[] { 11000 });
    public static RegionProfile Japan  { get; } = new("jp", Array.Empty<IpRange>(), Array.Empty<ushort>());
    public static RegionProfile Korea  { get; } = new("kr", Array.Empty<IpRange>(), Array.Empty<ushort>());
}
```

- [ ] **Step 4: Run tests, confirm pass**

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Core/Capture/RegionProfile.cs tests/Mabipacade.Core.Tests/Capture/RegionProfileTests.cs
git commit -m "feat(capture): add RegionProfile with Taiwan port-only profile"
```

---

### Task 16: NicSelector

Uses Win32 `GetBestInterface` to map a remote IP to the NIC index that would route to it, then matches that NIC against SharpPcap's device list.

**Files:**
- Create: `src/Mabipacade.Core/Capture/INicSelector.cs`
- Create: `src/Mabipacade.Core/Capture/Win32NicSelector.cs`

- [ ] **Step 1: Define interface**

`src/Mabipacade.Core/Capture/INicSelector.cs`:
```csharp
using System.Net;
using SharpPcap;

namespace Mabipacade.Core.Capture;

public interface INicSelector
{
    ICaptureDevice? SelectFor(IPAddress remote);
}
```

- [ ] **Step 2: Implement Win32 version**

`src/Mabipacade.Core/Capture/Win32NicSelector.cs`:
```csharp
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using SharpPcap;
using SharpPcap.LibPcap;

namespace Mabipacade.Core.Capture;

public sealed class Win32NicSelector : INicSelector
{
    public ICaptureDevice? SelectFor(IPAddress remote)
    {
        if (GetBestInterface(BitConverter.ToUInt32(remote.GetAddressBytes(), 0), out uint ifIndex) != 0)
            return null;

        var winNic = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(n => n.GetIPProperties().GetIPv4Properties()?.Index == ifIndex);
        if (winNic is null) return null;

        foreach (LibPcapLiveDevice d in LibPcapLiveDeviceList.Instance)
        {
            if (d.Interface.FriendlyName is { } friendly && friendly == winNic.Name)
                return d;
        }
        return null;
    }

    [DllImport("iphlpapi.dll", CharSet = CharSet.Auto)]
    private static extern int GetBestInterface(uint destAddr, out uint bestIfIndex);
}
```

- [ ] **Step 3: Build**

Run: `dotnet build`. Clean.

- [ ] **Step 4: Commit**

```
git add src/Mabipacade.Core/Capture/INicSelector.cs src/Mabipacade.Core/Capture/Win32NicSelector.cs
git commit -m "feat(capture): add Win32NicSelector via GetBestInterface"
```

---

### Task 17: GameEndpointResolver

Two-stage strategy:
1. Look up `Client.exe` PID via `ProcessFinder`. Query `ITcpConnectionTable` for `ESTABLISHED` outbound connections owned by that PID.
2. If process unavailable, fall back to `RegionProfile` filter: poll TCP table for any `ESTABLISHED` row whose remote endpoint is in the profile's ranges/ports.

If multiple candidates remain, prefer one not on ports 80/443 (filters out background HTTP).

**Files:**
- Create: `src/Mabipacade.Core/Capture/GameEndpoint.cs`
- Create: `src/Mabipacade.Core/Capture/GameEndpointResolver.cs`
- Create: `src/Mabipacade.Core/Capture/CaptureOptions.cs`
- Create: `tests/Mabipacade.Core.Tests/Capture/GameEndpointResolverTests.cs`

- [ ] **Step 1: Write the failing tests with mock TCP table**

```csharp
using System.Net;
using Mabipacade.Core.Capture;

namespace Mabipacade.Core.Tests.Capture;

public class GameEndpointResolverTests
{
    private sealed class FakeTcpTable : ITcpConnectionTable
    {
        public List<TcpConnectionRow> Rows { get; } = new();
        public IReadOnlyList<TcpConnectionRow> GetConnections() => Rows;
    }

    private static TcpConnectionRow Row(string remote, ushort port, int pid, TcpConnectionState state = TcpConnectionState.Established)
        => new(IPAddress.Loopback, 50000, IPAddress.Parse(remote), port, state, pid);

    [Fact]
    public void Resolves_FromPid_WhenSingleEstablished()
    {
        var tbl = new FakeTcpTable();
        tbl.Rows.Add(Row("61.218.1.2", 11000, pid: 4812));
        var resolver = new GameEndpointResolver(tbl, processPid: 4812, region: null);
        var ep = resolver.TryResolveOnce();
        Assert.NotNull(ep);
        Assert.Equal(IPAddress.Parse("61.218.1.2"), ep!.RemoteAddress);
        Assert.Equal((ushort)11000, ep.RemotePort);
    }

    [Fact]
    public void Prefers_NonWebPort_WhenMultipleEstablished()
    {
        var tbl = new FakeTcpTable();
        tbl.Rows.Add(Row("1.1.1.1", 443, pid: 4812));
        tbl.Rows.Add(Row("61.218.1.2", 11000, pid: 4812));
        var resolver = new GameEndpointResolver(tbl, processPid: 4812, region: null);
        var ep = resolver.TryResolveOnce();
        Assert.Equal((ushort)11000, ep!.RemotePort);
    }

    [Fact]
    public void Falls_BackToRegion_WhenPidUnknown()
    {
        var tbl = new FakeTcpTable();
        tbl.Rows.Add(Row("10.0.0.1", 11000, pid: 9999));
        var region = new RegionProfile("test",
            new[] { new IpRange(IPAddress.Parse("10.0.0.0"), IPAddress.Parse("10.0.0.255")) },
            new ushort[] { 11000 });
        var resolver = new GameEndpointResolver(tbl, processPid: null, region: region);
        var ep = resolver.TryResolveOnce();
        Assert.NotNull(ep);
        Assert.Equal((ushort)11000, ep!.RemotePort);
    }

    [Fact]
    public void Returns_Null_WhenNothingMatches()
    {
        var tbl = new FakeTcpTable();
        var resolver = new GameEndpointResolver(tbl, processPid: 4812, region: null);
        Assert.Null(resolver.TryResolveOnce());
    }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement `CaptureOptions`, `GameEndpoint`, `GameEndpointResolver`**

`src/Mabipacade.Core/Capture/GameEndpoint.cs`:
```csharp
using System.Net;

namespace Mabipacade.Core.Capture;

public sealed record GameEndpoint(
    int? ProcessId,
    IPAddress RemoteAddress,
    ushort RemotePort,
    IPAddress LocalAddress,
    ushort LocalPort);
```

`src/Mabipacade.Core/Capture/CaptureOptions.cs`:
```csharp
namespace Mabipacade.Core.Capture;

public sealed class CaptureOptions
{
    public string[] ProcessNames { get; init; } = new[] { "Client.exe" };
    public RegionProfile? Region { get; init; }
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);
    public bool PreferProcessDetection { get; init; } = true;
}
```

`src/Mabipacade.Core/Capture/GameEndpointResolver.cs`:
```csharp
using System.Net;

namespace Mabipacade.Core.Capture;

public sealed class GameEndpointResolver
{
    private readonly ITcpConnectionTable _table;
    private readonly int? _processPid;
    private readonly RegionProfile? _region;

    public GameEndpointResolver(ITcpConnectionTable table, int? processPid, RegionProfile? region)
    {
        _table = table;
        _processPid = processPid;
        _region = region;
    }

    public GameEndpoint? TryResolveOnce()
    {
        var rows = _table.GetConnections();

        IEnumerable<TcpConnectionRow> candidates = rows
            .Where(r => r.State == TcpConnectionState.Established);

        if (_processPid is int pid)
        {
            var owned = candidates.Where(r => r.OwningPid == pid).ToList();
            if (owned.Count > 0) return Pick(owned);
        }

        if (_region is not null)
        {
            var inRange = candidates.Where(r => _region.Contains(r.RemoteAddress, r.RemotePort)).ToList();
            if (inRange.Count > 0) return Pick(inRange);
        }

        return null;
    }

    private static GameEndpoint Pick(IReadOnlyList<TcpConnectionRow> rows)
    {
        var preferred = rows.FirstOrDefault(r => r.RemotePort is not 80 and not 443) ?? rows[0];
        return new GameEndpoint(
            preferred.OwningPid,
            preferred.RemoteAddress,
            preferred.RemotePort,
            preferred.LocalAddress,
            preferred.LocalPort);
    }
}
```

- [ ] **Step 4: Run tests, confirm 4 pass**

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Core/Capture/GameEndpoint.cs src/Mabipacade.Core/Capture/CaptureOptions.cs src/Mabipacade.Core/Capture/GameEndpointResolver.cs tests/Mabipacade.Core.Tests/Capture/GameEndpointResolverTests.cs
git commit -m "feat(capture): add GameEndpointResolver with PID + region fallback"
```

---

## Phase 5 — Recording / Replay

### Task 18: PcapWriter

Writes RawFrames to a `.pcap` file. Sits on the Stage 0 fan-out: pipeline gives every RawFrame to it before doing anything else.

**Files:**
- Create: `src/Mabipacade.Core/Recording/PcapWriter.cs`
- Create: `tests/Mabipacade.Core.Tests/Recording/PcapWriterTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Mabipacade.Core.Recording;
using PacketDotNet;
using SharpPcap.LibPcap;

namespace Mabipacade.Core.Tests.Recording;

public class PcapWriterTests
{
    [Fact]
    public void WrittenFrames_AreReadableBack()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mabipacade-test-{Guid.NewGuid():N}.pcap");
        try
        {
            using (var writer = new PcapWriter(path, LinkLayers.Ethernet))
            {
                writer.Write(new byte[] { 1, 2, 3, 4 }, DateTime.UtcNow);
                writer.Write(new byte[] { 5, 6, 7, 8 }, DateTime.UtcNow);
            }
            int read = 0;
            using var reader = new CaptureFileReaderDevice(path);
            reader.Open(new SharpPcap.DeviceConfiguration());
            reader.OnPacketArrival += (_, _) => read++;
            reader.Capture();
            reader.Close();
            Assert.Equal(2, read);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement**

`src/Mabipacade.Core/Recording/PcapWriter.cs`:
```csharp
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;

namespace Mabipacade.Core.Recording;

public sealed class PcapWriter : IDisposable
{
    private readonly CaptureFileWriterDevice _writer;
    private readonly LinkLayers _linkLayer;

    public PcapWriter(string path, LinkLayers linkLayer)
    {
        _linkLayer = linkLayer;
        _writer = new CaptureFileWriterDevice(path);
        _writer.Open(new DeviceConfiguration());
    }

    public void Write(byte[] frame, DateTime timestampUtc)
    {
        var header = new PacketHeader(
            (uint)((timestampUtc - DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerSecond),
            (uint)((timestampUtc.Ticks % TimeSpan.TicksPerSecond) / 10),
            (uint)frame.Length,
            (uint)frame.Length);
        _writer.Write(new RawCapture((PhysicalLayers)_linkLayer, header, frame));
    }

    public void Dispose() => _writer.Close();
}
```

**Engineer note:** If SharpPcap 6.3.1 exposes `CaptureFileWriterDevice.Write(RawCapture)` with a different signature, adapt. Verify by reading the SharpPcap source or `mabi_stage4_boss_notifier/MabiStage4Notifier.Core/Capture/PcapRecorder.cs`.

- [ ] **Step 4: Run test, confirm pass**

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Core/Recording/PcapWriter.cs tests/Mabipacade.Core.Tests/Recording/PcapWriterTests.cs
git commit -m "feat(recording): add PcapWriter with roundtrip test"
```

---

### Task 19: ReplayTransport (forward-only)

Wraps a `PcapFileFrameSource` and adds Play/Pause/Stop/Rate/Step/SeekForward semantics. The `Rate` parameter is honoured by sleeping between frames using the pcap-recorded inter-frame deltas multiplied by `1/Rate`.

**Files:**
- Create: `src/Mabipacade.Core/Replay/ReplayTransport.cs`
- Create: `tests/Mabipacade.Core.Tests/Replay/ReplayTransportTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Mabipacade.Core.Model;
using Mabipacade.Core.Replay;

namespace Mabipacade.Core.Tests.Replay;

public class ReplayTransportTests
{
    [Fact]
    public void Initial_StateIsStopped()
    {
        var transport = new ReplayTransport(new FakeReplaySource());
        Assert.Equal(ReplayState.Stopped, transport.State);
    }

    [Fact]
    public async Task Play_TransitionsToPlaying_AndEmits()
    {
        var src = new FakeReplaySource(
            (DateTime.UnixEpoch.AddSeconds(0), new byte[] { 1 }),
            (DateTime.UnixEpoch.AddSeconds(0.001), new byte[] { 2 }),
            (DateTime.UnixEpoch.AddSeconds(0.002), new byte[] { 3 }));
        var transport = new ReplayTransport(src) { Rate = 100 };
        var received = new List<byte[]>();
        transport.FrameEmitted += (_, e) => received.Add(e.Data);

        transport.Play();
        await transport.WaitForCompletionAsync();

        Assert.Equal(ReplayState.Stopped, transport.State);
        Assert.Equal(3, received.Count);
    }

    [Fact]
    public void SeekForwardTo_Earlier_Throws()
    {
        var transport = new ReplayTransport(new FakeReplaySource());
        Assert.Throws<ArgumentException>(() => transport.SeekForwardTo(TimeSpan.FromSeconds(-1)));
    }
}

internal sealed class FakeReplaySource : Mabipacade.Core.Sources.IFrameSource
{
    private readonly (DateTime ts, byte[] data)[] _frames;
    public FakeReplaySource(params (DateTime ts, byte[] data)[] frames) { _frames = frames; }
    public event EventHandler<Mabipacade.Core.Sources.RawFrameEventArgs>? FrameReceived;
    public event EventHandler? EndOfStream;
    public Task StartAsync(CancellationToken ct)
    {
        foreach (var (ts, data) in _frames)
            FrameReceived?.Invoke(this, new Mabipacade.Core.Sources.RawFrameEventArgs(data, PacketDotNet.LinkLayers.Ethernet, ts));
        EndOfStream?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
    public Task StopAsync() => Task.CompletedTask;
    public void Dispose() { }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement**

`src/Mabipacade.Core/Replay/ReplayTransport.cs`:
```csharp
using Mabipacade.Core.Sources;

namespace Mabipacade.Core.Replay;

public enum ReplayState { Stopped, Playing, Paused }

public sealed class ReplayTransport : IDisposable
{
    private readonly IFrameSource _source;
    private TaskCompletionSource? _completion;
    private CancellationTokenSource? _cts;

    public ReplayTransport(IFrameSource source)
    {
        _source = source;
        _source.FrameReceived += OnFrame;
        _source.EndOfStream += OnEos;
    }

    public ReplayState State { get; private set; } = ReplayState.Stopped;
    public TimeSpan Position { get; private set; } = TimeSpan.Zero;
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    public double Rate { get; set; } = 1.0;

    public event EventHandler<RawFrameEventArgs>? FrameEmitted;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<ReplayState>? StateChanged;

    public void Play()
    {
        if (State == ReplayState.Playing) return;
        State = ReplayState.Playing;
        StateChanged?.Invoke(this, State);
        _cts = new CancellationTokenSource();
        _completion = new TaskCompletionSource();
        _ = _source.StartAsync(_cts.Token);
    }

    public void Pause()
    {
        if (State == ReplayState.Playing) { State = ReplayState.Paused; StateChanged?.Invoke(this, State); }
    }

    public void Stop()
    {
        State = ReplayState.Stopped;
        _cts?.Cancel();
        _source.StopAsync();
        StateChanged?.Invoke(this, State);
        _completion?.TrySetResult();
    }

    public void StepForward(int n = 1)
    {
        // PcapFileFrameSource emits frames as fast as it can; "step" semantics
        // mean: pause after N frames have been emitted. Pause first, then bump
        // the counter, then resume in non-stepping mode.
        if (n < 1) return;
        Pause();
        _stepRemaining = n;
        Play();
    }

    private int _stepRemaining;

    public void SeekForwardTo(TimeSpan t)
    {
        if (t < Position) throw new ArgumentException("Cannot seek backward; reload pcap instead.");
        Position = t;
        PositionChanged?.Invoke(this, Position);
    }

    public Task WaitForCompletionAsync() => _completion?.Task ?? Task.CompletedTask;

    private void OnFrame(object? sender, RawFrameEventArgs e)
    {
        if (State != ReplayState.Playing) return;
        FrameEmitted?.Invoke(this, e);
        Position = e.TimestampUtc - DateTime.UnixEpoch;
        PositionChanged?.Invoke(this, Position);
        if (_stepRemaining > 0 && --_stepRemaining == 0) Pause();
    }

    private void OnEos(object? sender, EventArgs e)
    {
        State = ReplayState.Stopped;
        StateChanged?.Invoke(this, State);
        _completion?.TrySetResult();
    }

    public void Dispose()
    {
        _source.FrameReceived -= OnFrame;
        _source.EndOfStream -= OnEos;
        _cts?.Dispose();
    }
}
```

- [ ] **Step 4: Run tests, confirm pass**

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Core/Replay/ReplayTransport.cs tests/Mabipacade.Core.Tests/Replay/ReplayTransportTests.cs
git commit -m "feat(replay): add forward-only ReplayTransport"
```

---

## Phase 6 — Diagnostics / Events

### Task 20: SessionEvent hierarchy

**Files:**
- Create: `src/Mabipacade.Core/Diagnostics/SessionEvent.cs`
- Create: `tests/Mabipacade.Core.Tests/Diagnostics/SessionEventTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Net;
using Mabipacade.Core.Diagnostics;

namespace Mabipacade.Core.Tests.Diagnostics;

public class SessionEventTests
{
    [Fact]
    public void ConnectionResumed_ExposesSameAsLastFlag()
    {
        var ts = DateTime.UtcNow;
        var ev = new SessionEvent.ConnectionResumed(ts, new IPEndPoint(IPAddress.Parse("1.2.3.4"), 11000), SameAsLast: false);
        Assert.Equal(ts, ev.TimestampUtc);
        Assert.False(ev.SameAsLast);
    }

    [Fact]
    public void PatternMatching_DistinguishesVariants()
    {
        SessionEvent ev = new SessionEvent.SessionStart(DateTime.UtcNow, "tw", 4812);
        var name = ev switch
        {
            SessionEvent.SessionStart => "start",
            SessionEvent.SessionEnd => "end",
            SessionEvent.ConnectionLost => "lost",
            _ => "other"
        };
        Assert.Equal("start", name);
    }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement**

`src/Mabipacade.Core/Diagnostics/SessionEvent.cs`:
```csharp
using System.Net;

namespace Mabipacade.Core.Diagnostics;

public abstract record SessionEvent(DateTime TimestampUtc)
{
    public sealed record SessionStart(DateTime TimestampUtc, string Region, int? ProcessId)
        : SessionEvent(TimestampUtc);
    public sealed record SessionEnd(DateTime TimestampUtc, string Reason)
        : SessionEvent(TimestampUtc);
    public sealed record ConnectionEstablished(DateTime TimestampUtc, IPEndPoint Remote, string NicName)
        : SessionEvent(TimestampUtc);
    public sealed record ConnectionLost(DateTime TimestampUtc, IPEndPoint LastRemote)
        : SessionEvent(TimestampUtc);
    public sealed record ConnectionResumed(DateTime TimestampUtc, IPEndPoint NewRemote, bool SameAsLast)
        : SessionEvent(TimestampUtc);
    public sealed record FrameResync(DateTime TimestampUtc, long ByteOffset, string Reason)
        : SessionEvent(TimestampUtc);
    public sealed record BadBody(DateTime TimestampUtc, ushort Op, int Length)
        : SessionEvent(TimestampUtc);
    public sealed record DecoderFailed(DateTime TimestampUtc, ushort Op, string ExceptionMessage)
        : SessionEvent(TimestampUtc);
}
```

- [ ] **Step 4: Run tests, confirm pass**

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Core/Diagnostics/SessionEvent.cs tests/Mabipacade.Core.Tests/Diagnostics/SessionEventTests.cs
git commit -m "feat(diagnostics): add SessionEvent hierarchy"
```

---

### Task 21: PipelineMetrics

Simple atomic counters; pipeline updates, consumers poll.

**Files:**
- Create: `src/Mabipacade.Core/Diagnostics/PipelineMetrics.cs`
- Create: `tests/Mabipacade.Core.Tests/Diagnostics/PipelineMetricsTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Mabipacade.Core.Diagnostics;

namespace Mabipacade.Core.Tests.Diagnostics;

public class PipelineMetricsTests
{
    [Fact]
    public void Counters_StartAtZero()
    {
        var m = new PipelineMetrics();
        Assert.Equal(0, m.TotalFrames);
        Assert.Equal(0, m.TotalPackets);
        Assert.Equal(0, m.BadBodyCount);
        Assert.Equal(0, m.FrameResyncCount);
    }

    [Fact]
    public void Increments_AreThreadSafe()
    {
        var m = new PipelineMetrics();
        Parallel.For(0, 1000, _ => m.IncrementFrames());
        Assert.Equal(1000, m.TotalFrames);
    }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement**

`src/Mabipacade.Core/Diagnostics/PipelineMetrics.cs`:
```csharp
namespace Mabipacade.Core.Diagnostics;

public sealed class PipelineMetrics
{
    private long _frames, _packets, _badBody, _resync;

    public long TotalFrames => Interlocked.Read(ref _frames);
    public long TotalPackets => Interlocked.Read(ref _packets);
    public long BadBodyCount => Interlocked.Read(ref _badBody);
    public long FrameResyncCount => Interlocked.Read(ref _resync);

    public void IncrementFrames()   => Interlocked.Increment(ref _frames);
    public void IncrementPackets()  => Interlocked.Increment(ref _packets);
    public void IncrementBadBody()  => Interlocked.Increment(ref _badBody);
    public void IncrementResync()   => Interlocked.Increment(ref _resync);
}
```

- [ ] **Step 4: Run tests, confirm pass**

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Core/Diagnostics/PipelineMetrics.cs tests/Mabipacade.Core.Tests/Diagnostics/PipelineMetricsTests.cs
git commit -m "feat(diagnostics): add PipelineMetrics with atomic counters"
```

---

## Phase 7 — Pipeline facade

### Task 22: PacketPipeline (assembles Stage 0..7)

Wires `IFrameSource → PacketDotNet (TcpFrame) → TcpReassembler → MabiPacketFramer → MessageElemReader → DecoderRegistry → events`. Emits `MabiPacket` via `PacketReceived` and `SessionEvent`s via `SessionEventReceived`.

**Files:**
- Create: `src/Mabipacade.Core/Pipeline/PacketPipeline.cs`
- Create: `tests/Mabipacade.Core.Tests/Pipeline/PacketPipelineTests.cs`

- [ ] **Step 1: Write the failing test (with fake source emitting hand-crafted TCP payloads)**

```csharp
using PacketDotNet;
using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Sources;

namespace Mabipacade.Core.Tests.Pipeline;

public class PacketPipelineTests
{
    [Fact]
    public async Task EmitsPacket_ForSingleHandCraftedFrame()
    {
        // Build TCP/Eth/IP wrapping a one-packet Mabi payload.
        var mabiBytes = TestPacketBuilder.Build(op: 0x6984, entityId: 0UL, body: new byte[] { 0x02, 0x78, 0xE6 });
        var frame = TestEthernetBuilder.WrapTcp(mabiBytes, srcPort: 11000, dstPort: 50000);

        var source = new FakeFrameSource(frame);
        var registry = new DecoderRegistry();
        var pipeline = new PacketPipeline(source, registry);

        var packets = new List<Mabipacade.Core.Model.MabiPacket>();
        pipeline.PacketReceived += (_, p) => packets.Add(p);

        await pipeline.StartAsync(CancellationToken.None);
        await pipeline.StopAsync();

        Assert.Single(packets);
        Assert.Equal((ushort)0x6984, packets[0].Op);
    }
}

internal sealed class FakeFrameSource : Mabipacade.Core.Sources.IFrameSource
{
    private readonly byte[] _frame;
    public FakeFrameSource(byte[] frame) { _frame = frame; }
    public event EventHandler<RawFrameEventArgs>? FrameReceived;
    public event EventHandler? EndOfStream;
    public Task StartAsync(CancellationToken ct)
    {
        FrameReceived?.Invoke(this, new RawFrameEventArgs(_frame, PacketDotNet.LinkLayers.Ethernet, DateTime.UtcNow));
        EndOfStream?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
    public Task StopAsync() => Task.CompletedTask;
    public void Dispose() { }
}
```

Engineer also creates `TestEthernetBuilder` to wrap raw Mabi bytes inside Ethernet→IPv4→TCP (use PacketDotNet builders or hand-craft).

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement `PacketPipeline`**

`src/Mabipacade.Core/Pipeline/PacketPipeline.cs`:
```csharp
using PacketDotNet;
using Mabipacade.Core.Diagnostics;
using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Core.Sources;

namespace Mabipacade.Core.Pipeline;

public sealed class PacketPipeline : IDisposable
{
    private readonly IFrameSource _source;
    private readonly DecoderRegistry _decoders;
    private readonly TcpReassembler _reassembler = new();
    private readonly PipelineMetrics _metrics = new();

    public event EventHandler<MabiPacket>? PacketReceived;
    public event EventHandler<SessionEvent>? SessionEventReceived;
    public PipelineMetrics Metrics => _metrics;

    public PacketPipeline(IFrameSource source, DecoderRegistry decoders)
    {
        _source = source;
        _decoders = decoders;
        _source.FrameReceived += OnFrame;
        _source.EndOfStream += OnEos;
    }

    public Task StartAsync(CancellationToken ct) => _source.StartAsync(ct);
    public Task StopAsync() => _source.StopAsync();

    private void OnFrame(object? sender, RawFrameEventArgs e)
    {
        _metrics.IncrementFrames();
        var parsed = Packet.ParsePacket(e.LinkLayer, e.Data);
        var tcp = parsed.Extract<TcpPacket>();
        if (tcp?.PayloadData is not { Length: > 0 } payload) return;

        var ip = tcp.ParentPacket as IPPacket;
        var frame = new TcpFrame(
            ip?.SourceAddress ?? System.Net.IPAddress.None,
            tcp.SourcePort,
            ip?.DestinationAddress ?? System.Net.IPAddress.None,
            tcp.DestinationPort,
            tcp.SequenceNumber,
            payload,
            e.TimestampUtc);

        _reassembler.Feed(frame);
        DrainPackets(e.TimestampUtc);
    }

    private void DrainPackets(DateTime timestampUtc)
    {
        while (true)
        {
            var buf = _reassembler.GetBuffer();
            if (buf.Length == 0) return;

            var result = MabiPacketFramer.TryReadOne(buf, out var slice, out int consumed);
            switch (result)
            {
                case FrameResult.Ok:
                    _reassembler.Consume(consumed);
                    EmitPacket(slice!, timestampUtc);
                    break;
                case FrameResult.NeedMore:
                    return;
                case FrameResult.FramingError:
                    _metrics.IncrementResync();
                    SessionEventReceived?.Invoke(this, new SessionEvent.FrameResync(timestampUtc, 0, "framing error"));
                    _reassembler.Reset();
                    return;
            }
        }
    }

    private void EmitPacket(MabiPacketSlice slice, DateTime timestampUtc)
    {
        var elemResult = MessageElemReader.TryRead(slice.Body, out var elems);
        if (elemResult == ReadElemsResult.BadBody)
        {
            _metrics.IncrementBadBody();
            SessionEventReceived?.Invoke(this, new SessionEvent.BadBody(timestampUtc, slice.Op, slice.Body.Length));
            return;
        }

        object? decoded = null;
        if (_decoders.TryGet(slice.Op, out var decoder) && decoder is not null)
        {
            try
            {
                decoded = decoder.Decode(new DecoderInput(timestampUtc, Direction.Inbound, slice.Op, slice.EntityId, elems));
            }
            catch (Exception ex)
            {
                SessionEventReceived?.Invoke(this, new SessionEvent.DecoderFailed(timestampUtc, slice.Op, ex.Message));
            }
        }

        _metrics.IncrementPackets();
        PacketReceived?.Invoke(this, new MabiPacket(timestampUtc, Direction.Inbound, slice.Op, slice.EntityId, elems, decoded));
    }

    private void OnEos(object? sender, EventArgs e)
    {
        SessionEventReceived?.Invoke(this, new SessionEvent.SessionEnd(DateTime.UtcNow, "EndOfStream"));
    }

    public void Dispose()
    {
        _source.FrameReceived -= OnFrame;
        _source.EndOfStream -= OnEos;
    }
}
```

- [ ] **Step 4: Run tests, confirm pass**

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Core/Pipeline/PacketPipeline.cs tests/Mabipacade.Core.Tests/Pipeline/PacketPipelineTests.cs tests/Mabipacade.Core.Tests/Pipeline/FakeFrameSource.cs tests/Mabipacade.Core.Tests/Pipeline/TestEthernetBuilder.cs
git commit -m "feat(pipeline): add PacketPipeline facade wiring Stage 0..7"
```

---

### Task 22b: Reconnect loop in PacketPipeline

Adds a background task that polls `ITcpConnectionTable` every `CaptureOptions.PollInterval` to detect disconnect, then triggers re-resolution. Emits `ConnectionLost` → `ConnectionResumed` (with `SameAsLast` flag) per spec §4. Reset `TcpReassembler` on disconnect; re-issue BPF filter on `SameAsLast = false`.

Only relevant for `LiveFrameSource` path — pcap replay doesn't reconnect. Architecturally, the loop is a sibling of the pipeline (it owns the source), not the pipeline itself.

**Files:**
- Create: `src/Mabipacade.Core/Capture/CaptureSession.cs`
- Create: `tests/Mabipacade.Core.Tests/Capture/CaptureSessionTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Net;
using Mabipacade.Core.Capture;
using Mabipacade.Core.Diagnostics;

namespace Mabipacade.Core.Tests.Capture;

public class CaptureSessionTests
{
    private sealed class FakeTable : ITcpConnectionTable
    {
        public List<TcpConnectionRow> Rows { get; } = new();
        public IReadOnlyList<TcpConnectionRow> GetConnections() => Rows;
    }

    private static TcpConnectionRow Row(string ip, ushort port, int pid)
        => new(IPAddress.Loopback, 50000, IPAddress.Parse(ip), port, TcpConnectionState.Established, pid);

    [Fact]
    public void EmitsLost_WhenEndpointDisappears()
    {
        var tbl = new FakeTable();
        tbl.Rows.Add(Row("61.218.1.2", 11000, pid: 4812));
        var session = new CaptureSession(tbl, processPid: 4812, region: null);
        var events = new List<SessionEvent>();
        session.SessionEventReceived += (_, e) => events.Add(e);

        session.PollOnce(); // initial: emits ConnectionEstablished
        tbl.Rows.Clear();   // simulate disconnect
        session.PollOnce(); // emits ConnectionLost

        Assert.Contains(events, e => e is SessionEvent.ConnectionEstablished);
        Assert.Contains(events, e => e is SessionEvent.ConnectionLost);
    }

    [Fact]
    public void EmitsResumed_SameAsLastTrue_WhenSameEndpointReturns()
    {
        var tbl = new FakeTable();
        var session = new CaptureSession(tbl, processPid: 4812, region: null);
        var events = new List<SessionEvent>();
        session.SessionEventReceived += (_, e) => events.Add(e);

        tbl.Rows.Add(Row("61.218.1.2", 11000, pid: 4812));
        session.PollOnce();
        tbl.Rows.Clear();
        session.PollOnce();
        tbl.Rows.Add(Row("61.218.1.2", 11000, pid: 4812));  // same IP+port
        session.PollOnce();

        var resumed = events.OfType<SessionEvent.ConnectionResumed>().Single();
        Assert.True(resumed.SameAsLast);
    }

    [Fact]
    public void EmitsResumed_SameAsLastFalse_WhenNewEndpoint()
    {
        var tbl = new FakeTable();
        var session = new CaptureSession(tbl, processPid: 4812, region: null);
        var events = new List<SessionEvent>();
        session.SessionEventReceived += (_, e) => events.Add(e);

        tbl.Rows.Add(Row("61.218.1.2", 11000, pid: 4812));
        session.PollOnce();
        tbl.Rows.Clear();
        session.PollOnce();
        tbl.Rows.Add(Row("61.218.1.3", 11000, pid: 4812));  // different IP — channel change
        session.PollOnce();

        var resumed = events.OfType<SessionEvent.ConnectionResumed>().Single();
        Assert.False(resumed.SameAsLast);
    }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement**

`src/Mabipacade.Core/Capture/CaptureSession.cs`:
```csharp
using System.Net;
using Mabipacade.Core.Diagnostics;

namespace Mabipacade.Core.Capture;

public sealed class CaptureSession
{
    private readonly GameEndpointResolver _resolver;
    private GameEndpoint? _current;
    private IPEndPoint? _lastRemote;

    public event EventHandler<SessionEvent>? SessionEventReceived;

    public CaptureSession(ITcpConnectionTable table, int? processPid, RegionProfile? region)
    {
        _resolver = new GameEndpointResolver(table, processPid, region);
    }

    public GameEndpoint? Current => _current;

    public void PollOnce()
    {
        var next = _resolver.TryResolveOnce();
        if (next is null && _current is not null)
        {
            SessionEventReceived?.Invoke(this, new SessionEvent.ConnectionLost(
                DateTime.UtcNow, new IPEndPoint(_current.RemoteAddress, _current.RemotePort)));
            _lastRemote = new IPEndPoint(_current.RemoteAddress, _current.RemotePort);
            _current = null;
            return;
        }
        if (next is not null && _current is null)
        {
            var nextRemote = new IPEndPoint(next.RemoteAddress, next.RemotePort);
            if (_lastRemote is null)
                SessionEventReceived?.Invoke(this, new SessionEvent.ConnectionEstablished(DateTime.UtcNow, nextRemote, ""));
            else
                SessionEventReceived?.Invoke(this, new SessionEvent.ConnectionResumed(
                    DateTime.UtcNow, nextRemote, SameAsLast: nextRemote.Equals(_lastRemote)));
            _current = next;
        }
    }

    public async Task RunAsync(TimeSpan interval, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            PollOnce();
            try { await Task.Delay(interval, ct); }
            catch (OperationCanceledException) { return; }
        }
    }
}
```

- [ ] **Step 4: Run tests, confirm pass**

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Core/Capture/CaptureSession.cs tests/Mabipacade.Core.Tests/Capture/CaptureSessionTests.cs
git commit -m "feat(capture): add CaptureSession reconnect loop with SameAsLast flag"
```

---

## Phase 8 — Decoders (24 ops)

Each decoder lives in its own file under `src/Mabipacade.Decoders/<Category>/`. Each task in this phase adds **all** decoders in one category — POCO + decoder + test for each op. The pattern is the same; the wire-format specifics differ per op.

**For every decoder:**
1. Define POCO `record` with the field(s) extracted (or empty marker if body shape unknown).
2. Implement `IPacketDecoder` returning the POCO.
3. Write a unit test that builds a synthetic `DecoderInput` and asserts the POCO matches expected output.

**Where body shape is unverified** (most ops outside the skill chain & EntityAppear), the POCO is an empty marker — `public sealed record SomeEvent;`. The decoder still registers, so `MabiPacket.Decoded` is the POCO instance (acts as a typed "this is event X" signal). Consumers who need fields can wait for verification.

### Task 23: OpCodes enum

`src/Mabipacade.Decoders/OpCodes.cs`:
```csharp
namespace Mabipacade.Decoders;

public enum OpCodes : ushort
{
    CombatAction          = 0x7924,
    CombatActionEnd       = 0x7925,
    CombatActionPack      = 0x7926,
    PlayerSkillPrepareStart    = 0x6984,
    PlayerSkillPrepareReady    = 0x6985,
    PlayerSkillPostCastAck1    = 0x6988,
    PlayerSkillPostCastAck2    = 0x6989,
    PlayerSkillStop            = 0x698B,
    PlayerSkillPrepareProgress = 0x6993,
    EntityAppear        = 0x520C,
    EntityDisappear     = 0x520D,
    EntitiesAppear      = 0x5334,
    EntitiesDisappear   = 0x5335,
    IsNowDead           = 0x53FC,
    StatUpdatePrivate   = 0x7530,
    StatUpdatePublic    = 0x7532,
    EntityRelated       = 0x7534,
    ConditionUpdate2    = 0xA028,
    Chat                = 0x526C,
    Effect              = 0x9091,
    EffectDelayed       = 0x9095,
    SharpMind           = 0xA41E,
    PartyWindowUpdate   = 0xA43C,
    EquipmentChanged    = 0x59E6
}
```

- [ ] **Step 1: Create file, build, commit**

```
git add src/Mabipacade.Decoders/OpCodes.cs
git commit -m "feat(decoders): add OpCodes enum (24 ops)"
```

---

### Task 24: Skills decoders (6 ops — verified, highest priority)

These are the most documented in the notes; do these first so the pipeline can be E2E-tested on a real skill-chain pcap.

For each of the 6 ops below, do the full TDD cycle (test → fail → implement → pass → commit). Group commits per op so git history is readable.

#### 24a — PlayerSkillPrepareStart (0x6984)

**POCO:** `public sealed record PlayerSkillPrepareStart(ushort SkillId);`

**Body shape:** `msg[0] = Short(SkillId)`.

`tests/Mabipacade.Decoders.Tests/Skills/PlayerSkillPrepareStartDecoderTests.cs`:
```csharp
using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Skills;

namespace Mabipacade.Decoders.Tests.Skills;

public class PlayerSkillPrepareStartDecoderTests
{
    [Fact]
    public void DecodesSkillId_FromFirstElem()
    {
        var decoder = new PlayerSkillPrepareStartDecoder();
        var input = new DecoderInput(
            DateTime.UtcNow, Direction.Inbound, 0x6984, 12345UL,
            new[] { MessageElem.Short(59000) });
        var result = (PlayerSkillPrepareStart)decoder.Decode(input);
        Assert.Equal((ushort)59000, result.SkillId);
    }

    [Fact]
    public void Op_Is6984()
    {
        Assert.Equal((ushort)0x6984, new PlayerSkillPrepareStartDecoder().Op);
    }
}
```

`src/Mabipacade.Decoders/Skills/PlayerSkillPrepareStartDecoder.cs`:
```csharp
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Skills;

public sealed record PlayerSkillPrepareStart(ushort SkillId);

public sealed class PlayerSkillPrepareStartDecoder : IPacketDecoder
{
    public ushort Op => 0x6984;
    public object Decode(DecoderInput input) =>
        new PlayerSkillPrepareStart(input.Elems[0].AsUInt16());
}
```

- [ ] Run test, pass, commit `feat(decoders): add PlayerSkillPrepareStart (0x6984)`

#### 24b — PlayerSkillPrepareReady (0x6985), PostCastAck1 (0x6988), PostCastAck2 (0x6989)

Same shape as 24a (SkillId in `msg[0]`). Three near-identical POCO+decoder+test triples. Engineer follows the 24a template, one commit per op.

#### 24c — PlayerSkillPrepareProgress (0x6993) **trap: SkillId at msg[2]**

`src/Mabipacade.Decoders/Skills/PlayerSkillPrepareProgressDecoder.cs`:
```csharp
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Skills;

public sealed record PlayerSkillPrepareProgress(ushort SkillId);

public sealed class PlayerSkillPrepareProgressDecoder : IPacketDecoder
{
    public ushort Op => 0x6993;
    // SkillId is at msg[2], not msg[0] — protocol quirk documented in
    // mabinogi-packet-decoding/README.md (section "Player skill chain").
    public object Decode(DecoderInput input) =>
        new PlayerSkillPrepareProgress(input.Elems[2].AsUInt16());
}
```

The one comment is justified because it's a non-obvious trap (the WHY rule kicks in).

#### 24d — PlayerSkillStop (0x698B) **no SkillId**

```csharp
public sealed record PlayerSkillStop;

public sealed class PlayerSkillStopDecoder : IPacketDecoder
{
    public ushort Op => 0x698B;
    public object Decode(DecoderInput input) => new PlayerSkillStop();
}
```

Test verifies it returns `PlayerSkillStop` instance regardless of elems content.

- [ ] **All 6 ops committed individually**

---

### Task 25: Combat decoders (3 ops)

#### 25a — CombatActionPack (0x7926) **complex, sub-packet structure**

Reference body layout: `D:/Projects/mabi_stage4_boss_notifier/MabiStage4Notifier.Core/Packet/Packets/CombatActionPacketParser.cs`. Read it carefully.

**POCO:**
```csharp
public sealed record CombatActionPack(
    ulong AttackerId,
    IReadOnlyList<CombatSubAction> Sub);

public sealed record CombatSubAction(
    byte Type,
    ushort SkillId,
    ushort SubSkillId,
    ulong TargetId,
    int Damage);
```

**Decoder:**
Implement by parsing `input.Elems` per the layout in `CombatActionPacketParser.cs`. Each sub-packet is a fixed-shape block within the Bin-type elem(s). Engineer's job is to translate that parser to the new POCO. Test with hand-built `DecoderInput` of 1 sub-action then 3 sub-actions.

#### 25b — CombatAction (0x7924), CombatActionEnd (0x7925) **shape unverified**

Empty marker POCO:
```csharp
public sealed record CombatAction;
public sealed record CombatActionEnd;
```

Tests just assert correct op binding.

- [ ] **3 ops committed individually**

---

### Task 26: Entity decoders (5 ops)

#### 26a — EntityAppear (0x520C)

Body contains `RaceId` and `Name` (per notes). Reference shape: `mabi_stage4_boss_notifier/MabiStage4Notifier.Core/Packet/Packets/EntityAppearPacket.cs`.

```csharp
public sealed record EntityAppear(uint RaceId, string Name);
```

Engineer extracts these from `input.Elems` based on reference impl. Test with 2 cases (typical entity, boss entity).

#### 26b — EntityDisappear, EntitiesAppear, EntitiesDisappear, IsNowDead

Empty marker POCOs:
```csharp
public sealed record EntityDisappear;
public sealed record EntitiesAppear;
public sealed record EntitiesDisappear;
public sealed record IsNowDead;
```

- [ ] **5 ops committed individually**

---

### Task 27: Stat decoders (4 ops — all empty marker)

```csharp
public sealed record StatUpdatePrivate;     // 0x7530
public sealed record StatUpdatePublic;      // 0x7532
public sealed record EntityRelated;         // 0x7534
public sealed record ConditionUpdate2;      // 0xA028
```

- [ ] **4 ops committed individually**

---

### Task 28: Misc decoders (6 ops — all empty marker except Chat)

```csharp
public sealed record Chat(string Sender, string Message);  // shape speculative, see note below
public sealed record Effect;
public sealed record EffectDelayed;
public sealed record SharpMind;
public sealed record PartyWindowUpdate;
public sealed record EquipmentChanged;
```

For Chat: shape is **speculative**; populate fields only if `input.Elems` has the expected `String + String` pattern, else return `new Chat("", "")`. Test confirms two-string case + empty fallback.

- [ ] **6 ops committed individually**

---

### Task 29: DefaultDecoders helper

One-call registration of all 24 decoders.

**Files:**
- Create: `src/Mabipacade.Decoders/DefaultDecoders.cs`
- Create: `tests/Mabipacade.Decoders.Tests/DefaultDecodersTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Mabipacade.Core.Pipeline;
using Mabipacade.Decoders;

namespace Mabipacade.Decoders.Tests;

public class DefaultDecodersTests
{
    [Fact]
    public void RegisterAll_Adds24Decoders()
    {
        var reg = new DecoderRegistry();
        DefaultDecoders.RegisterAll(reg);
        Assert.Equal(24, reg.RegisteredOps.Count);
    }

    [Fact]
    public void RegisterAll_IncludesKeyOps()
    {
        var reg = new DecoderRegistry();
        DefaultDecoders.RegisterAll(reg);
        Assert.Contains((ushort)0x6984, reg.RegisteredOps);
        Assert.Contains((ushort)0x7926, reg.RegisteredOps);
        Assert.Contains((ushort)0x520C, reg.RegisteredOps);
    }
}
```

- [ ] **Step 2: Implement**

`src/Mabipacade.Decoders/DefaultDecoders.cs`:
```csharp
using Mabipacade.Core.Pipeline;
using Mabipacade.Decoders.Combat;
using Mabipacade.Decoders.Entity;
using Mabipacade.Decoders.Misc;
using Mabipacade.Decoders.Skills;
using Mabipacade.Decoders.Stats;

namespace Mabipacade.Decoders;

public static class DefaultDecoders
{
    public static void RegisterAll(DecoderRegistry registry)
    {
        registry.Register(new CombatActionDecoder());
        registry.Register(new CombatActionEndDecoder());
        registry.Register(new CombatActionPackDecoder());

        registry.Register(new PlayerSkillPrepareStartDecoder());
        registry.Register(new PlayerSkillPrepareReadyDecoder());
        registry.Register(new PlayerSkillPostCastAck1Decoder());
        registry.Register(new PlayerSkillPostCastAck2Decoder());
        registry.Register(new PlayerSkillStopDecoder());
        registry.Register(new PlayerSkillPrepareProgressDecoder());

        registry.Register(new EntityAppearDecoder());
        registry.Register(new EntityDisappearDecoder());
        registry.Register(new EntitiesAppearDecoder());
        registry.Register(new EntitiesDisappearDecoder());
        registry.Register(new IsNowDeadDecoder());

        registry.Register(new StatUpdatePrivateDecoder());
        registry.Register(new StatUpdatePublicDecoder());
        registry.Register(new EntityRelatedDecoder());
        registry.Register(new ConditionUpdate2Decoder());

        registry.Register(new ChatDecoder());
        registry.Register(new EffectDecoder());
        registry.Register(new EffectDelayedDecoder());
        registry.Register(new SharpMindDecoder());
        registry.Register(new PartyWindowUpdateDecoder());
        registry.Register(new EquipmentChangedDecoder());
    }
}
```

- [ ] **Step 3: Run tests, confirm pass**

- [ ] **Step 4: Commit**

```
git add src/Mabipacade.Decoders/DefaultDecoders.cs tests/Mabipacade.Decoders.Tests/DefaultDecodersTests.cs
git commit -m "feat(decoders): add DefaultDecoders.RegisterAll one-call helper"
```

---

## Phase 9 — E2E pcap regression tests

### Task 30: End-to-end pcap pipeline test

Loads a real pcap, runs the full pipeline with all decoders registered, asserts non-zero packets + zero BadBody / FrameResync incidents.

**Files:**
- Create: `tests/Mabipacade.Core.Tests/EndToEnd/PipelineE2ETests.cs`
- Update: `tests/Mabipacade.Core.Tests/fixtures/README.md` to instruct copying 1 known-good pcap

- [ ] **Step 1: Document fixture requirement**

Update `tests/Mabipacade.Core.Tests/fixtures/README.md`:
```markdown
# Test fixtures

These pcap files are loaded by E2E tests but NOT committed to git
(see top-level `.gitignore`). To run the E2E suite locally:

1. Copy one pcap from `D:/Projects/mabi_stage4_boss_notifier/publish/logs/`
   to `tests/Mabipacade.Core.Tests/fixtures/known_good.pcap`
2. Remove `[Fact(Skip = "Requires fixture")]` attribute on E2E tests
3. Run `dotnet test`

The CI build runs without fixtures (tests are skipped); local dev catches
regressions when running with fixtures present.
```

- [ ] **Step 2: Write E2E test**

`tests/Mabipacade.Core.Tests/EndToEnd/PipelineE2ETests.cs`:
```csharp
using Mabipacade.Core.Diagnostics;
using Mabipacade.Core.Model;
using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Sources;
using Mabipacade.Decoders;

namespace Mabipacade.Core.Tests.EndToEnd;

public class PipelineE2ETests
{
    private const string FixturePath = "fixtures/known_good.pcap";

    [Fact(Skip = "Requires local fixture")]
    public async Task FullPipeline_ProducesPackets_FromRealPcap()
    {
        if (!File.Exists(FixturePath)) return;

        using var source = new PcapFileFrameSource(FixturePath);
        var registry = new DecoderRegistry();
        DefaultDecoders.RegisterAll(registry);
        var pipeline = new PacketPipeline(source, registry);

        var packets = new List<MabiPacket>();
        var events  = new List<SessionEvent>();
        pipeline.PacketReceived += (_, p) => packets.Add(p);
        pipeline.SessionEventReceived += (_, e) => events.Add(e);

        await pipeline.StartAsync(CancellationToken.None);
        // Wait for EOS event
        await Task.Delay(1000);
        await pipeline.StopAsync();

        Assert.True(packets.Count > 0, "expected at least one decoded packet");
        Assert.Empty(events.OfType<SessionEvent.FrameResync>());
        // Some BadBody is acceptable (unknown ops), but excessive is suspicious:
        int badBodyRate = events.OfType<SessionEvent.BadBody>().Count();
        Assert.True(badBodyRate < packets.Count, "more BadBody than packets — framer likely broken");
    }
}
```

- [ ] **Step 3: Verify test discovers + skips when fixture absent**

Run:
```
dotnet test tests/Mabipacade.Core.Tests --filter "FullyQualifiedName~PipelineE2ETests"
```
Expected: 1 skipped, 0 failed.

- [ ] **Step 4: Verify locally with fixture**

Engineer copies `known_good.pcap` into `fixtures/`, removes `Skip`, runs again. Expected: passes with > 0 packets emitted, 0 FrameResync. Re-add `Skip` before commit.

- [ ] **Step 5: Commit**

```
git add tests/Mabipacade.Core.Tests/EndToEnd/ tests/Mabipacade.Core.Tests/fixtures/README.md
git commit -m "test(e2e): add pcap pipeline regression test"
```

---

### Task 31: Run the full test suite & green check

- [ ] **Step 1: Run all tests**

Run:
```
dotnet test
```
Expected: all tests pass (skipped fixtures are OK).

- [ ] **Step 2: Verify no warnings**

Run:
```
dotnet build /warnaserror
```
Expected: 0 errors, 0 warnings.

- [ ] **Step 3: Final commit if any clean-up needed**

If there are stray .cs files or csproj inconsistencies, fix them here.

```
git add -A
git status   # review before committing
git commit -m "chore: M1 final clean-up"   # only if there are changes
```

---

## Done conditions for M1

When this plan finishes, the following must hold:

- [x] `dotnet build` clean (`TreatWarningsAsErrors=true`)
- [x] `dotnet test` green (skipped fixture tests OK)
- [x] All 24 decoders registered and individually unit-tested
- [x] `PacketPipeline` consumes a `IFrameSource` and emits `MabiPacket` events
- [x] `ReplayTransport` plays/pauses/steps forward over a pcap file
- [x] `GameEndpointResolver` finds Client.exe + falls back to region profile
- [x] `CaptureSession` polls TCP table, emits ConnectionLost/Resumed with SameAsLast flag
- [x] E2E test loads a real pcap and produces non-zero MabiPackets with zero FrameResync
- [x] git history readable: ~30 small commits, each green at build time

The deliverable is two NuGet-publishable libraries (`Mabipacade.Core`, `Mabipacade.Decoders`). C# consumers can:

```csharp
using var source = new PcapFileFrameSource("session.pcap");
var registry = new DecoderRegistry();
DefaultDecoders.RegisterAll(registry);
var pipeline = new PacketPipeline(source, registry);

pipeline.PacketReceived += (_, p) =>
{
    if (p.Decoded is PlayerSkillPrepareStart prep)
        Console.WriteLine($"prep skill {prep.SkillId}");
};

await pipeline.StartAsync(CancellationToken.None);
```

That's the baseline. M2 (`Mabipacade.Cli`) builds NDJSON output on top; M3 (`Mabipacade.DebugUi`) builds WPF GUI on top; M4 (`Mabipacade.Server`) builds WebSocket on top. Each is a separate plan.

---

## Open follow-ups for M2+ (not addressed in M1)

- Region profile IP ranges (currently `Taiwan` has empty ranges with port 11000 filter only)
- Outbound direction implementation (type predefined, decoder support pending)
- Wiring `CaptureSession` into the live capture path (currently emits events but doesn't yet swap `LiveFrameSource` filter on reconnect — that goes with M2 CLI integration)
- Pcap timestamp precision in `PcapWriter` (sub-millisecond accuracy)
- Decoders for ops outside the 24-op notes table

These belong to M2 or later milestones' plans.
