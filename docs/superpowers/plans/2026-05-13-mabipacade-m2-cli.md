# Mabipacade M2: Cli (NDJSON sidecar) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `Mabipacade.Cli` — a console exe with `capture` (live → NDJSON) and `replay` (pcap → NDJSON) subcommands, plus optional session recording (`session.pcap` + `session.events.ndjson` + `session.json`), so cross-language consumers (Go, Python, JS, e.g. `mabidilmeter-mogu-2`) can subscribe to Mabinogi packets via subprocess + stdout pipe.

**Architecture:** A thin shell on top of M1's `Mabipacade.Core` + `Mabipacade.Decoders`. Subscribes to `PacketPipeline.PacketReceived` and `PacketPipeline.SessionEventReceived`, serializes each to a single-line JSON envelope with a `kind` discriminator, writes to stdout (one line per event). Session recording is optional and writes three sidecar files to a target directory. stdout is reserved for NDJSON output; stderr is reserved for log lines (strict contract for downstream parsers).

**Tech Stack:** .NET 10 · C# 13 · xUnit · `System.CommandLine` 2.0.0-beta5.25306.1 · `System.Text.Json` (built-in) · existing `SharpPcap` / `PacketDotNet` (via Core).

**Reference material:**
- Design spec: `docs/superpowers/specs/2026-05-13-mabipacade-design.md` (§3 NDJSON, §5 file outputs, §6.5 mixed stream)
- M1 plan: `docs/superpowers/plans/2026-05-13-mabipacade-m1-core-decoders.md`
- M1 commits: `672246e..a8e275d` (Core + Decoders + per-flow fix)
- Pcap fixture (gitignored): `tests/Mabipacade.Core.Tests/fixtures/known_good.pcap`

---

## File Structure

```
src/Mabipacade.Cli/
├── Mabipacade.Cli.csproj
├── Program.cs                       # System.CommandLine root
├── Commands/
│   ├── CaptureCommand.cs            # live capture, optional recording
│   ├── ReplayCommand.cs             # pcap → NDJSON
│   └── CommonOptions.cs             # shared option definitions
├── Output/
│   ├── NdjsonWriter.cs              # MabiPacket / SessionEvent → one JSON line
│   ├── ElemJson.cs                  # MessageElem → JSON token
│   ├── EnvelopeShape.cs             # serialization helpers (camelCase, hex op, string uint64)
│   └── OpCodeNames.cs               # OpCodes enum → name lookup
├── Recording/
│   ├── SessionRecorder.cs           # owns pcap + events.ndjson + session.json
│   └── SessionMetadata.cs           # the session.json record shape
├── Filters/
│   ├── OpFilter.cs                  # --filter-op parsing + matching
│   └── DiagnosticsLevel.cs          # --diagnostics off/on/summary
└── Logging/
    └── StderrLogger.cs              # plain stderr log

tests/Mabipacade.Cli.Tests/
├── Mabipacade.Cli.Tests.csproj
├── Output/
│   ├── NdjsonWriterTests.cs
│   ├── ElemJsonTests.cs
│   ├── EnvelopeShapeTests.cs
│   └── OpCodeNamesTests.cs
├── Recording/
│   └── SessionRecorderTests.cs
├── Filters/
│   ├── OpFilterTests.cs
│   └── DiagnosticsLevelTests.cs
├── Commands/
│   └── ReplayCommandTests.cs        # E2E (skipped without fixture)
└── Schema/
    └── NdjsonContractTests.cs       # parses stdout, validates schema
```

**File ownership rule:** one class per file (with the exception of `EnvelopeShape.cs` which contains a few tightly-coupled helper records). `Program.cs` only wires `RootCommand`; all real work happens in `Commands/`. `Output/` types know nothing about `Commands/` (kept reusable).

---

## Conventions

- **stdout = pure NDJSON.** One JSON object per line, terminated by `\n`. Never write log lines, banners, or progress to stdout. The contract is enforced by `NdjsonContractTests`.
- **stderr = log only.** Plain text, free format. `StderrLogger` is the only writer.
- **JSON property naming:** camelCase via `JsonNamingPolicy.CamelCase`.
- **JSON file output (session.json):** indented for human readability. NDJSON output: unindented.
- **`uint64` (entityId) is serialized as JSON string.** JS / many languages lose precision above 2^53 on numeric parse.
- **`op` is serialized as hex string `"0xXXXX"`**, not a number. Avoids enum-decimal traps from M1 notes.
- **All commits include `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>` trailer.**

---

## Phase 0 — Project scaffolding

### Task 1: Create Mabipacade.Cli + Mabipacade.Cli.Tests projects

**Files:**
- Create: `src/Mabipacade.Cli/Mabipacade.Cli.csproj`
- Create: `src/Mabipacade.Cli/Program.cs`
- Create: `tests/Mabipacade.Cli.Tests/Mabipacade.Cli.Tests.csproj`

- [ ] **Step 1: Create the console exe project**

Run:
```
dotnet new console -n Mabipacade.Cli -o src/Mabipacade.Cli -f net10.0
dotnet sln add src/Mabipacade.Cli/Mabipacade.Cli.csproj
dotnet add src/Mabipacade.Cli reference src/Mabipacade.Core
dotnet add src/Mabipacade.Cli reference src/Mabipacade.Decoders
```

- [ ] **Step 2: Configure csproj**

Replace `src/Mabipacade.Cli/Mabipacade.Cli.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AssemblyName>mabipacade</AssemblyName>
    <RootNamespace>Mabipacade.Cli</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.CommandLine" Version="2.0.0-beta5.25306.1" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Mabipacade.Core\Mabipacade.Core.csproj" />
    <ProjectReference Include="..\Mabipacade.Decoders\Mabipacade.Decoders.csproj" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Mabipacade.Cli.Tests" />
  </ItemGroup>
</Project>
```

`AssemblyName=mabipacade` makes the exe `mabipacade.exe` (matches the spec's user-facing name).

- [ ] **Step 3: Write minimal Program.cs**

Replace `src/Mabipacade.Cli/Program.cs` with:

```csharp
using System.CommandLine;

namespace Mabipacade.Cli;

public static class Program
{
    public static Task<int> Main(string[] args)
    {
        var root = new RootCommand("Mabipacade CLI — Mabinogi packet sidecar");
        return root.Parse(args).InvokeAsync();
    }
}
```

This compiles to a no-op CLI that just prints `--help` when run with no args; subcommands are added in later tasks.

- [ ] **Step 4: Create the test project**

Run:
```
dotnet new xunit -n Mabipacade.Cli.Tests -o tests/Mabipacade.Cli.Tests -f net10.0
dotnet sln add tests/Mabipacade.Cli.Tests/Mabipacade.Cli.Tests.csproj
dotnet add tests/Mabipacade.Cli.Tests reference src/Mabipacade.Core
dotnet add tests/Mabipacade.Cli.Tests reference src/Mabipacade.Decoders
dotnet add tests/Mabipacade.Cli.Tests reference src/Mabipacade.Cli
```
Delete `UnitTest1.cs` from `tests/Mabipacade.Cli.Tests/`.

- [ ] **Step 5: Verify build**

```
dotnet build
```
Expected: 0 errors, 0 warnings.

- [ ] **Step 6: Verify CLI runs**

```
dotnet run --project src/Mabipacade.Cli -- --help
```
Expected: prints help banner including "Mabipacade CLI — Mabinogi packet sidecar".

- [ ] **Step 7: Commit**

```
git add src/Mabipacade.Cli/ tests/Mabipacade.Cli.Tests/ mabipacade.sln
git commit -m "$(cat <<'EOF'
chore(cli): scaffold Mabipacade.Cli + Mabipacade.Cli.Tests

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 1 — JSON serialization layer

### Task 2: ElemJson (MessageElem → JSON token)

Compact `{"t":"<Type>","v":<value>}` per elem. `uint64`/`int64` → string. `Bin` → base64. Float kept as JSON number.

**Files:**
- Create: `src/Mabipacade.Cli/Output/ElemJson.cs`
- Create: `tests/Mabipacade.Cli.Tests/Output/ElemJsonTests.cs`

- [ ] **Step 1: Write failing tests**

`tests/Mabipacade.Cli.Tests/Output/ElemJsonTests.cs`:
```csharp
using System.Text.Json;
using Mabipacade.Cli.Output;
using Mabipacade.Core.Model;

namespace Mabipacade.Cli.Tests.Output;

public class ElemJsonTests
{
    private static string Render(MessageElem e)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            ElemJson.Write(w, e);
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    [Fact] public void Byte_AsObject() =>
        Assert.Equal("{\"t\":\"Byte\",\"v\":42}", Render(MessageElem.Byte(42)));

    [Fact] public void Short_AsObject() =>
        Assert.Equal("{\"t\":\"Short\",\"v\":59000}", Render(MessageElem.Short(59000)));

    [Fact] public void Int_AsObject() =>
        Assert.Equal("{\"t\":\"Int\",\"v\":1234567}", Render(MessageElem.Int(1234567u)));

    [Fact] public void Long_AsString_ForUint64Safety() =>
        Assert.Equal("{\"t\":\"Long\",\"v\":\"18446744073709551615\"}", Render(MessageElem.Long(ulong.MaxValue)));

    [Fact] public void Float_AsNumber() =>
        Assert.Equal("{\"t\":\"Float\",\"v\":1.5}", Render(MessageElem.Float(1.5f)));

    [Fact] public void String_AsObject() =>
        Assert.Equal("{\"t\":\"String\",\"v\":\"hello\"}", Render(MessageElem.String("hello")));

    [Fact] public void Bin_AsBase64() =>
        Assert.Equal("{\"t\":\"Bin\",\"v\":\"AQID\"}", Render(MessageElem.Bin(new byte[] { 1, 2, 3 })));
}
```

- [ ] **Step 2: Run, confirm fail**

Run: `dotnet test tests/Mabipacade.Cli.Tests`
Expected: compile error (`ElemJson` not found).

- [ ] **Step 3: Implement**

`src/Mabipacade.Cli/Output/ElemJson.cs`:
```csharp
using System.Text.Json;
using Mabipacade.Core.Model;

namespace Mabipacade.Cli.Output;

internal static class ElemJson
{
    public static void Write(Utf8JsonWriter w, MessageElem e)
    {
        w.WriteStartObject();
        w.WriteString("t", e.Type.ToString());
        switch (e.Type)
        {
            case MessageElemType.Byte:   w.WriteNumber("v", e.AsByte()); break;
            case MessageElemType.Short:  w.WriteNumber("v", e.AsUInt16()); break;
            case MessageElemType.Int:    w.WriteNumber("v", e.AsUInt32()); break;
            case MessageElemType.Long:   w.WriteString("v", e.AsUInt64().ToString()); break;
            case MessageElemType.Float:  w.WriteNumber("v", e.AsFloat()); break;
            case MessageElemType.String: w.WriteString("v", e.AsString()); break;
            case MessageElemType.Bin:    w.WriteBase64String("v", e.AsBytes()); break;
        }
        w.WriteEndObject();
    }
}
```

- [ ] **Step 4: Run, confirm 7 tests pass**

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Cli/Output/ElemJson.cs tests/Mabipacade.Cli.Tests/Output/ElemJsonTests.cs
git commit -m "feat(cli): add ElemJson with type-safe MessageElem serialization"
```

---

### Task 3: OpCodeNames (op → friendly name)

Looks up an op against `Mabipacade.Decoders.OpCodes` enum; returns the enum name or `null`.

**Files:**
- Create: `src/Mabipacade.Cli/Output/OpCodeNames.cs`
- Create: `tests/Mabipacade.Cli.Tests/Output/OpCodeNamesTests.cs`

- [ ] **Step 1: Failing test**

`tests/Mabipacade.Cli.Tests/Output/OpCodeNamesTests.cs`:
```csharp
using Mabipacade.Cli.Output;

namespace Mabipacade.Cli.Tests.Output;

public class OpCodeNamesTests
{
    [Fact]
    public void Known_Op_ReturnsEnumName()
    {
        Assert.Equal("PlayerSkillPrepareStart", OpCodeNames.TryGetName(0x6984));
        Assert.Equal("EntityAppear", OpCodeNames.TryGetName(0x520C));
        Assert.Equal("CombatActionPack", OpCodeNames.TryGetName(0x7926));
    }

    [Fact]
    public void Unknown_Op_ReturnsNull()
    {
        Assert.Null(OpCodeNames.TryGetName(0xFFFF));
        Assert.Null(OpCodeNames.TryGetName(0x1234));
    }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement**

`src/Mabipacade.Cli/Output/OpCodeNames.cs`:
```csharp
using Mabipacade.Decoders;

namespace Mabipacade.Cli.Output;

internal static class OpCodeNames
{
    private static readonly Dictionary<ushort, string> _names = Enum
        .GetValues<OpCodes>()
        .ToDictionary(o => (ushort)o, o => o.ToString());

    public static string? TryGetName(ushort op) =>
        _names.TryGetValue(op, out var name) ? name : null;
}
```

- [ ] **Step 4: Run, confirm 2 tests pass**

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Cli/Output/OpCodeNames.cs tests/Mabipacade.Cli.Tests/Output/OpCodeNamesTests.cs
git commit -m "feat(cli): add OpCodeNames lookup against Decoders.OpCodes enum"
```

---

### Task 4: EnvelopeShape (MabiPacket + SessionEvent → JSON envelope)

The keystone of the contract. Each public method writes a single JSON object (no trailing newline) using `Utf8JsonWriter`. The caller (NdjsonWriter) wraps in a line terminator.

**Files:**
- Create: `src/Mabipacade.Cli/Output/EnvelopeShape.cs`
- Create: `tests/Mabipacade.Cli.Tests/Output/EnvelopeShapeTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
using System.Text.Json;
using Mabipacade.Cli.Output;
using Mabipacade.Core.Diagnostics;
using Mabipacade.Core.Model;
using System.Net;

namespace Mabipacade.Cli.Tests.Output;

public class EnvelopeShapeTests
{
    private static string RenderPacket(MabiPacket p)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            EnvelopeShape.WritePacket(w, p);
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string RenderEvent(SessionEvent ev)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            EnvelopeShape.WriteEvent(w, ev);
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    [Fact]
    public void Packet_ShapeAndFields()
    {
        var ts = new DateTime(2026, 5, 13, 8, 23, 11, 842, DateTimeKind.Utc);
        var p = new MabiPacket(ts, Direction.Inbound, 0x6984, 12345UL,
            new[] { MessageElem.Short(59000) }, Decoded: null);
        var json = RenderPacket(p);

        Assert.Contains("\"kind\":\"packet\"", json);
        Assert.Contains("\"ts\":\"2026-05-13T08:23:11.842Z\"", json);
        Assert.Contains("\"dir\":\"in\"", json);
        Assert.Contains("\"op\":\"0x6984\"", json);
        Assert.Contains("\"opName\":\"PlayerSkillPrepareStart\"", json);
        Assert.Contains("\"entityId\":\"12345\"", json);
        Assert.Contains("\"type\":null", json);
        Assert.Contains("\"decoded\":null", json);
        Assert.Contains("\"elems\":[", json);
    }

    [Fact]
    public void Packet_WithDecoded_IncludesTypeAndPayload()
    {
        var ts = DateTime.UtcNow;
        var pocoLike = new { skillId = 59000 };
        var p = new MabiPacket(ts, Direction.Inbound, 0x6984, 1UL,
            Array.Empty<MessageElem>(), Decoded: pocoLike);
        var json = RenderPacket(p);

        Assert.Contains("\"type\":\"", json);
        Assert.Contains("\"decoded\":{\"skillId\":59000}", json);
    }

    [Fact]
    public void Packet_UnknownOp_OpNameNull()
    {
        var p = new MabiPacket(DateTime.UtcNow, Direction.Inbound, 0xFFFF, 0UL,
            Array.Empty<MessageElem>(), null);
        var json = RenderPacket(p);
        Assert.Contains("\"opName\":null", json);
    }

    [Fact]
    public void Event_SessionStart_ShapeAndFields()
    {
        var ts = new DateTime(2026, 5, 13, 8, 23, 11, DateTimeKind.Utc);
        var ev = new SessionEvent.SessionStart(ts, "tw", 4812);
        var json = RenderEvent(ev);

        Assert.Contains("\"kind\":\"event\"", json);
        Assert.Contains("\"type\":\"SessionStart\"", json);
        Assert.Contains("\"region\":\"tw\"", json);
        Assert.Contains("\"processId\":4812", json);
    }

    [Fact]
    public void Event_ConnectionResumed_IncludesSameAsLast()
    {
        var ev = new SessionEvent.ConnectionResumed(DateTime.UtcNow,
            new IPEndPoint(IPAddress.Parse("61.218.1.2"), 11000), SameAsLast: false);
        var json = RenderEvent(ev);

        Assert.Contains("\"type\":\"ConnectionResumed\"", json);
        Assert.Contains("\"newRemote\":\"61.218.1.2:11000\"", json);
        Assert.Contains("\"sameAsLast\":false", json);
    }

    [Fact]
    public void Event_BadBody_IncludesOpAndLength()
    {
        var ev = new SessionEvent.BadBody(DateTime.UtcNow, 0x9093, 42);
        var json = RenderEvent(ev);
        Assert.Contains("\"type\":\"BadBody\"", json);
        Assert.Contains("\"op\":\"0x9093\"", json);
        Assert.Contains("\"length\":42", json);
    }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement**

`src/Mabipacade.Cli/Output/EnvelopeShape.cs`:
```csharp
using System.Net;
using System.Text.Json;
using Mabipacade.Core.Diagnostics;
using Mabipacade.Core.Model;

namespace Mabipacade.Cli.Output;

internal static class EnvelopeShape
{
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static void WritePacket(Utf8JsonWriter w, MabiPacket p)
    {
        w.WriteStartObject();
        w.WriteString("kind", "packet");
        w.WriteString("ts", p.TimestampUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        w.WriteString("dir", p.Direction == Direction.Inbound ? "in" : "out");
        w.WriteString("op", $"0x{p.Op:X4}");

        var name = OpCodeNames.TryGetName(p.Op);
        if (name is null) w.WriteNull("opName"); else w.WriteString("opName", name);

        w.WriteString("entityId", p.EntityId.ToString());

        if (p.Decoded is null)
        {
            w.WriteNull("type");
            w.WriteNull("decoded");
        }
        else
        {
            w.WriteString("type", p.Decoded.GetType().Name);
            w.WritePropertyName("decoded");
            JsonSerializer.Serialize(w, p.Decoded, p.Decoded.GetType(), PayloadOptions);
        }

        w.WritePropertyName("elems");
        w.WriteStartArray();
        foreach (var e in p.Elems) ElemJson.Write(w, e);
        w.WriteEndArray();

        w.WriteEndObject();
    }

    public static void WriteEvent(Utf8JsonWriter w, SessionEvent ev)
    {
        w.WriteStartObject();
        w.WriteString("kind", "event");
        w.WriteString("ts", ev.TimestampUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        w.WriteString("type", ev.GetType().Name);

        switch (ev)
        {
            case SessionEvent.SessionStart s:
                w.WriteString("region", s.Region);
                if (s.ProcessId is null) w.WriteNull("processId"); else w.WriteNumber("processId", s.ProcessId.Value);
                break;
            case SessionEvent.SessionEnd s:
                w.WriteString("reason", s.Reason);
                break;
            case SessionEvent.ConnectionEstablished s:
                w.WriteString("remote", FormatEndpoint(s.Remote));
                w.WriteString("nicName", s.NicName);
                break;
            case SessionEvent.ConnectionLost s:
                w.WriteString("lastRemote", FormatEndpoint(s.LastRemote));
                break;
            case SessionEvent.ConnectionResumed s:
                w.WriteString("newRemote", FormatEndpoint(s.NewRemote));
                w.WriteBoolean("sameAsLast", s.SameAsLast);
                break;
            case SessionEvent.FrameResync s:
                w.WriteNumber("byteOffset", s.ByteOffset);
                w.WriteString("reason", s.Reason);
                break;
            case SessionEvent.BadBody s:
                w.WriteString("op", $"0x{s.Op:X4}");
                w.WriteNumber("length", s.Length);
                break;
            case SessionEvent.DecoderFailed s:
                w.WriteString("op", $"0x{s.Op:X4}");
                w.WriteString("exceptionMessage", s.ExceptionMessage);
                break;
        }

        w.WriteEndObject();
    }

    private static string FormatEndpoint(IPEndPoint ep) => $"{ep.Address}:{ep.Port}";
}
```

- [ ] **Step 4: Run, confirm 6 tests pass**

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Cli/Output/EnvelopeShape.cs tests/Mabipacade.Cli.Tests/Output/EnvelopeShapeTests.cs
git commit -m "feat(cli): add EnvelopeShape for packet + session-event JSON encoding"
```

---

### Task 5: NdjsonWriter (single-line writer with newline terminator)

Thin facade over `EnvelopeShape` that adds `\n` after each object and writes to a `TextWriter` (stdout in production, `StringWriter` in tests).

**Files:**
- Create: `src/Mabipacade.Cli/Output/NdjsonWriter.cs`
- Create: `tests/Mabipacade.Cli.Tests/Output/NdjsonWriterTests.cs`

- [ ] **Step 1: Failing test**

```csharp
using System.Text;
using Mabipacade.Cli.Output;
using Mabipacade.Core.Diagnostics;
using Mabipacade.Core.Model;

namespace Mabipacade.Cli.Tests.Output;

public class NdjsonWriterTests
{
    [Fact]
    public void WritesOneLinePerPacket_WithTrailingNewline()
    {
        var sb = new StringBuilder();
        using (var sw = new StringWriter(sb))
        {
            var writer = new NdjsonWriter(sw);
            writer.WritePacket(new MabiPacket(
                new DateTime(2026, 5, 13, 0, 0, 0, DateTimeKind.Utc),
                Direction.Inbound, 0x6984, 1UL, Array.Empty<MessageElem>(), null));
            writer.WritePacket(new MabiPacket(
                new DateTime(2026, 5, 13, 0, 0, 1, DateTimeKind.Utc),
                Direction.Inbound, 0x6985, 2UL, Array.Empty<MessageElem>(), null));
        }
        var lines = sb.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("{", lines[0]);
        Assert.EndsWith("}", lines[0]);
    }

    [Fact]
    public void Packets_And_Events_PreserveOrder()
    {
        var sb = new StringBuilder();
        using (var sw = new StringWriter(sb))
        {
            var writer = new NdjsonWriter(sw);
            writer.WriteEvent(new SessionEvent.SessionStart(DateTime.UtcNow, "tw", null));
            writer.WritePacket(new MabiPacket(DateTime.UtcNow, Direction.Inbound, 0x6984, 0UL, Array.Empty<MessageElem>(), null));
            writer.WriteEvent(new SessionEvent.SessionEnd(DateTime.UtcNow, "UserStop"));
        }
        var lines = sb.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.Contains("\"kind\":\"event\"", lines[0]);
        Assert.Contains("\"kind\":\"packet\"", lines[1]);
        Assert.Contains("\"kind\":\"event\"", lines[2]);
    }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement**

`src/Mabipacade.Cli/Output/NdjsonWriter.cs`:
```csharp
using System.Text.Json;
using Mabipacade.Core.Diagnostics;
using Mabipacade.Core.Model;

namespace Mabipacade.Cli.Output;

internal sealed class NdjsonWriter
{
    private readonly TextWriter _out;
    private readonly object _lock = new();

    public NdjsonWriter(TextWriter outWriter) { _out = outWriter; }

    public void WritePacket(MabiPacket p) => WriteLine(w => EnvelopeShape.WritePacket(w, p));
    public void WriteEvent(SessionEvent ev) => WriteLine(w => EnvelopeShape.WriteEvent(w, ev));

    private void WriteLine(Action<Utf8JsonWriter> writeBody)
    {
        using var ms = new MemoryStream();
        using (var jw = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false }))
        {
            writeBody(jw);
        }
        var line = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        lock (_lock)
        {
            _out.Write(line);
            _out.Write('\n');
        }
    }
}
```

`lock` serializes pipeline events arriving from background threads with manual `WritePacket`/`WriteEvent` calls; per the spec, pipeline events fire from a single thread, so contention is minimal — the lock is defensive insurance only.

- [ ] **Step 4: Run, confirm 2 tests pass**

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Cli/Output/NdjsonWriter.cs tests/Mabipacade.Cli.Tests/Output/NdjsonWriterTests.cs
git commit -m "feat(cli): add NdjsonWriter with thread-safe single-line emission"
```

---

## Phase 2 — Session recording

### Task 6: SessionMetadata record

The shape of `session.json` (written at session end).

**Files:**
- Create: `src/Mabipacade.Cli/Recording/SessionMetadata.cs`
- Create: `tests/Mabipacade.Cli.Tests/Recording/SessionMetadataTests.cs`

- [ ] **Step 1: Failing test**

```csharp
using System.Text.Json;
using Mabipacade.Cli.Recording;

namespace Mabipacade.Cli.Tests.Recording;

public class SessionMetadataTests
{
    [Fact]
    public void Serializes_WithCamelCase_AndIndented()
    {
        var meta = new SessionMetadata(
            Id: "2026-05-13T18-23-11",
            StartedAt: new DateTime(2026, 5, 13, 18, 23, 11, DateTimeKind.Utc),
            EndedAt: new DateTime(2026, 5, 13, 18, 50, 0, DateTimeKind.Utc),
            Region: "tw",
            Endpoints: new[]
            {
                new SessionEndpointRecord("61.218.1.2:11000",
                    new DateTime(2026, 5, 13, 18, 23, 11, DateTimeKind.Utc),
                    new DateTime(2026, 5, 13, 18, 45, 12, DateTimeKind.Utc))
            },
            Stats: new SessionStats(184231, 92117, 14, 2));

        var json = SessionMetadata.ToJson(meta);
        Assert.Contains("\"id\": \"2026-05-13T18-23-11\"", json);
        Assert.Contains("\"startedAt\": \"2026-05-13T18:23:11", json);
        Assert.Contains("\"region\": \"tw\"", json);
        Assert.Contains("\"totalFrames\": 184231", json);
        Assert.Contains("\"badBodyCount\": 14", json);
    }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement**

`src/Mabipacade.Cli/Recording/SessionMetadata.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mabipacade.Cli.Recording;

internal sealed record SessionMetadata(
    string Id,
    DateTime StartedAt,
    DateTime? EndedAt,
    string Region,
    IReadOnlyList<SessionEndpointRecord> Endpoints,
    SessionStats Stats)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string ToJson(SessionMetadata m) => JsonSerializer.Serialize(m, Options);
}

internal sealed record SessionEndpointRecord(string Remote, DateTime From, DateTime? To);

internal sealed record SessionStats(long TotalFrames, long TotalPackets, long BadBodyCount, long FramingResyncCount);
```

- [ ] **Step 4: Run, confirm test passes**

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Cli/Recording/SessionMetadata.cs tests/Mabipacade.Cli.Tests/Recording/SessionMetadataTests.cs
git commit -m "feat(cli): add SessionMetadata record for session.json"
```

---

### Task 7: SessionRecorder

Owns three sidecar files in a session directory: `session.pcap` (raw frames), `session.events.ndjson` (session events), `session.json` (summary). Subscribes to pipeline events; raw frames come from a separate hook on the source. The recorder is created with the directory path + `IFrameSource` + `PacketPipeline` and exposes `Stop()` which finalizes `session.json`.

**Files:**
- Create: `src/Mabipacade.Cli/Recording/SessionRecorder.cs`
- Create: `tests/Mabipacade.Cli.Tests/Recording/SessionRecorderTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
using System.Text.Json;
using Mabipacade.Cli.Recording;
using Mabipacade.Core.Diagnostics;
using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Sources;
using Mabipacade.Decoders;
using PacketDotNet;

namespace Mabipacade.Cli.Tests.Recording;

public class SessionRecorderTests
{
    [Fact]
    public void CreatesThreeFiles_InSessionDirectory()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"mp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            using var source = new TestFrameSource();
            var registry = new DecoderRegistry();
            DefaultDecoders.RegisterAll(registry);
            var pipeline = new PacketPipeline(source, registry);
            var recorder = new SessionRecorder(temp, region: "tw", processId: null,
                source: source, pipeline: pipeline, linkLayer: LinkLayers.Ethernet);

            recorder.Start();
            // No frames; just verify the recorder produces the right shape on Stop.
            recorder.Stop("UserStop");

            Assert.True(File.Exists(Path.Combine(temp, "session.pcap")));
            Assert.True(File.Exists(Path.Combine(temp, "session.events.ndjson")));
            Assert.True(File.Exists(Path.Combine(temp, "session.json")));

            var meta = File.ReadAllText(Path.Combine(temp, "session.json"));
            Assert.Contains("\"region\": \"tw\"", meta);
            Assert.Contains("\"endedAt\":", meta);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    [Fact]
    public void SessionEvents_LandInEventsFile()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"mp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            using var source = new TestFrameSource();
            var pipeline = new PacketPipeline(source, new DecoderRegistry());
            var recorder = new SessionRecorder(temp, region: "tw", processId: null,
                source: source, pipeline: pipeline, linkLayer: LinkLayers.Ethernet);
            recorder.Start();

            // Simulate a session event via the recorder's pipeline subscription path.
            // We invoke the recorder's event handler indirectly by raising on pipeline.
            // Since SessionEvent is fired from pipeline, we use reflection or a hook.
            // Simpler: the recorder also exposes WriteEvent for tests.
            recorder.WriteEvent(new SessionEvent.ConnectionLost(DateTime.UtcNow,
                new System.Net.IPEndPoint(System.Net.IPAddress.Parse("1.2.3.4"), 11000)));

            recorder.Stop("EOS");

            var events = File.ReadAllText(Path.Combine(temp, "session.events.ndjson"));
            Assert.Contains("\"type\":\"ConnectionLost\"", events);
            Assert.Contains("\"type\":\"SessionStart\"", events);
            Assert.Contains("\"type\":\"SessionEnd\"", events);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }
}

internal sealed class TestFrameSource : IFrameSource
{
    public event EventHandler<RawFrameEventArgs>? FrameReceived;
    public event EventHandler? EndOfStream;
    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
    public Task StopAsync() => Task.CompletedTask;
    public void Dispose() { }
    public void EmitFrame(byte[] data, DateTime ts) =>
        FrameReceived?.Invoke(this, new RawFrameEventArgs(data, LinkLayers.Ethernet, ts));
    public void EmitEos() => EndOfStream?.Invoke(this, EventArgs.Empty);
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement**

`src/Mabipacade.Cli/Recording/SessionRecorder.cs`:
```csharp
using System.Net;
using System.Text;
using Mabipacade.Cli.Output;
using Mabipacade.Core.Diagnostics;
using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Recording;
using Mabipacade.Core.Sources;
using PacketDotNet;

namespace Mabipacade.Cli.Recording;

internal sealed class SessionRecorder : IDisposable
{
    private readonly string _dir;
    private readonly string _region;
    private readonly int? _processId;
    private readonly IFrameSource _source;
    private readonly PacketPipeline _pipeline;
    private readonly PcapWriter _pcap;
    private readonly StreamWriter _events;
    private readonly NdjsonWriter _eventsWriter;
    private readonly DateTime _startedAt = DateTime.UtcNow;
    private readonly List<SessionEndpointRecord> _endpoints = new();
    private DateTime? _endedAt;
    private string? _currentRemote;
    private DateTime _currentSince;

    public SessionRecorder(string dir, string region, int? processId,
        IFrameSource source, PacketPipeline pipeline, LinkLayers linkLayer)
    {
        _dir = dir;
        _region = region;
        _processId = processId;
        _source = source;
        _pipeline = pipeline;
        Directory.CreateDirectory(dir);
        _pcap = new PcapWriter(Path.Combine(dir, "session.pcap"), linkLayer);
        _events = new StreamWriter(Path.Combine(dir, "session.events.ndjson"), append: false, Encoding.UTF8);
        _eventsWriter = new NdjsonWriter(_events);
    }

    public void Start()
    {
        _source.FrameReceived += OnFrame;
        _pipeline.SessionEventReceived += OnEvent;
        _eventsWriter.WriteEvent(new SessionEvent.SessionStart(_startedAt, _region, _processId));
    }

    public void Stop(string reason)
    {
        _endedAt = DateTime.UtcNow;
        if (_currentRemote is not null)
        {
            _endpoints.Add(new SessionEndpointRecord(_currentRemote, _currentSince, _endedAt));
            _currentRemote = null;
        }
        _eventsWriter.WriteEvent(new SessionEvent.SessionEnd(_endedAt.Value, reason));
        _source.FrameReceived -= OnFrame;
        _pipeline.SessionEventReceived -= OnEvent;
        _events.Flush();
        _events.Dispose();
        _pcap.Dispose();
        WriteSessionJson();
    }

    public void WriteEvent(SessionEvent ev) => _eventsWriter.WriteEvent(ev);

    private void OnFrame(object? sender, RawFrameEventArgs e) =>
        _pcap.Write(e.Data, e.TimestampUtc);

    private void OnEvent(object? sender, SessionEvent ev)
    {
        _eventsWriter.WriteEvent(ev);
        switch (ev)
        {
            case SessionEvent.ConnectionEstablished s:
                _currentRemote = $"{s.Remote.Address}:{s.Remote.Port}";
                _currentSince = s.TimestampUtc;
                break;
            case SessionEvent.ConnectionLost s:
                if (_currentRemote is not null)
                {
                    _endpoints.Add(new SessionEndpointRecord(_currentRemote, _currentSince, s.TimestampUtc));
                    _currentRemote = null;
                }
                break;
            case SessionEvent.ConnectionResumed s:
                _currentRemote = $"{s.NewRemote.Address}:{s.NewRemote.Port}";
                _currentSince = s.TimestampUtc;
                break;
        }
    }

    private void WriteSessionJson()
    {
        var meta = new SessionMetadata(
            Id: _startedAt.ToString("yyyy-MM-ddTHH-mm-ss"),
            StartedAt: _startedAt,
            EndedAt: _endedAt,
            Region: _region,
            Endpoints: _endpoints,
            Stats: new SessionStats(
                _pipeline.Metrics.TotalFrames,
                _pipeline.Metrics.TotalPackets,
                _pipeline.Metrics.BadBodyCount,
                _pipeline.Metrics.FrameResyncCount));
        File.WriteAllText(Path.Combine(_dir, "session.json"), SessionMetadata.ToJson(meta), Encoding.UTF8);
    }

    public void Dispose()
    {
        // Defensive: if Stop wasn't called, finalize gracefully.
        if (_endedAt is null) Stop("Disposed");
    }
}
```

- [ ] **Step 4: Run, confirm 2 tests pass**

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Cli/Recording/SessionRecorder.cs tests/Mabipacade.Cli.Tests/Recording/SessionRecorderTests.cs
git commit -m "feat(cli): add SessionRecorder owning pcap + events.ndjson + session.json"
```

---

## Phase 3 — Filters

### Task 8: OpFilter (parses `--filter-op 0x6984,0x6985`)

**Files:**
- Create: `src/Mabipacade.Cli/Filters/OpFilter.cs`
- Create: `tests/Mabipacade.Cli.Tests/Filters/OpFilterTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
using Mabipacade.Cli.Filters;

namespace Mabipacade.Cli.Tests.Filters;

public class OpFilterTests
{
    [Fact]
    public void Empty_AllowsEverything()
    {
        var f = OpFilter.Parse(null);
        Assert.True(f.Allows(0x6984));
        Assert.True(f.Allows(0xFFFF));
    }

    [Fact]
    public void Hex_CommaSeparated_LimitsToList()
    {
        var f = OpFilter.Parse("0x6984,0x7926");
        Assert.True(f.Allows(0x6984));
        Assert.True(f.Allows(0x7926));
        Assert.False(f.Allows(0x6985));
    }

    [Fact]
    public void Whitespace_Tolerated()
    {
        var f = OpFilter.Parse("0x6984 , 0x7926");
        Assert.True(f.Allows(0x6984));
        Assert.True(f.Allows(0x7926));
    }

    [Fact]
    public void InvalidHex_Throws()
    {
        Assert.Throws<FormatException>(() => OpFilter.Parse("0xZZZZ"));
    }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement**

`src/Mabipacade.Cli/Filters/OpFilter.cs`:
```csharp
using System.Globalization;

namespace Mabipacade.Cli.Filters;

internal sealed class OpFilter
{
    private readonly HashSet<ushort>? _allowed;

    private OpFilter(HashSet<ushort>? allowed) { _allowed = allowed; }

    public static OpFilter Parse(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return new OpFilter(null);
        var set = new HashSet<ushort>();
        foreach (var raw in spec.Split(','))
        {
            var token = raw.Trim();
            if (token.Length == 0) continue;
            if (!token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                throw new FormatException($"Op '{token}' must be hex like 0xXXXX");
            var hex = token[2..];
            if (!ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var op))
                throw new FormatException($"Op '{token}' is not a valid hex ushort");
            set.Add(op);
        }
        return new OpFilter(set.Count == 0 ? null : set);
    }

    public bool Allows(ushort op) => _allowed is null || _allowed.Contains(op);
}
```

- [ ] **Step 4: Run, confirm 4 tests pass**

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Cli/Filters/OpFilter.cs tests/Mabipacade.Cli.Tests/Filters/OpFilterTests.cs
git commit -m "feat(cli): add OpFilter for --filter-op parsing"
```

---

### Task 9: DiagnosticsLevel enum + parsing

`--diagnostics off|on|summary`. `off` (default) suppresses `FrameResync`, `BadBody`, `DecoderFailed` on stdout. `on` passes them through. `summary` would aggregate (deferred to v2; for M2 it behaves the same as `on` but we still parse it for forward compat).

**Files:**
- Create: `src/Mabipacade.Cli/Filters/DiagnosticsLevel.cs`
- Create: `tests/Mabipacade.Cli.Tests/Filters/DiagnosticsLevelTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
using Mabipacade.Cli.Filters;
using Mabipacade.Core.Diagnostics;

namespace Mabipacade.Cli.Tests.Filters;

public class DiagnosticsLevelTests
{
    [Fact]
    public void Off_SuppressesDiagnosticEvents()
    {
        Assert.False(DiagnosticsLevel.Off.PassesLive(new SessionEvent.BadBody(DateTime.UtcNow, 0x6984, 10)));
        Assert.False(DiagnosticsLevel.Off.PassesLive(new SessionEvent.FrameResync(DateTime.UtcNow, 0, "x")));
        Assert.False(DiagnosticsLevel.Off.PassesLive(new SessionEvent.DecoderFailed(DateTime.UtcNow, 0x6984, "boom")));
    }

    [Fact]
    public void Off_PassesNonDiagnosticEvents()
    {
        Assert.True(DiagnosticsLevel.Off.PassesLive(new SessionEvent.SessionStart(DateTime.UtcNow, "tw", null)));
        Assert.True(DiagnosticsLevel.Off.PassesLive(new SessionEvent.ConnectionLost(DateTime.UtcNow,
            new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 11000))));
    }

    [Fact]
    public void On_PassesEverything()
    {
        Assert.True(DiagnosticsLevel.On.PassesLive(new SessionEvent.BadBody(DateTime.UtcNow, 0x6984, 10)));
    }

    [Fact]
    public void Parse_OffOnSummary()
    {
        Assert.Equal(DiagnosticsLevel.Off, DiagnosticsLevel.Parse("off"));
        Assert.Equal(DiagnosticsLevel.On, DiagnosticsLevel.Parse("on"));
        Assert.Equal(DiagnosticsLevel.Summary, DiagnosticsLevel.Parse("summary"));
        Assert.Equal(DiagnosticsLevel.Off, DiagnosticsLevel.Parse(null));
    }

    [Fact]
    public void Parse_Invalid_Throws()
    {
        Assert.Throws<FormatException>(() => DiagnosticsLevel.Parse("loud"));
    }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement**

`src/Mabipacade.Cli/Filters/DiagnosticsLevel.cs`:
```csharp
using Mabipacade.Core.Diagnostics;

namespace Mabipacade.Cli.Filters;

internal sealed class DiagnosticsLevel
{
    public static readonly DiagnosticsLevel Off = new("off", suppressDiagnostic: true);
    public static readonly DiagnosticsLevel On = new("on", suppressDiagnostic: false);
    public static readonly DiagnosticsLevel Summary = new("summary", suppressDiagnostic: false);

    private readonly bool _suppressDiagnostic;
    public string Name { get; }

    private DiagnosticsLevel(string name, bool suppressDiagnostic)
    {
        Name = name;
        _suppressDiagnostic = suppressDiagnostic;
    }

    public bool PassesLive(SessionEvent ev) => !_suppressDiagnostic || !IsDiagnostic(ev);

    private static bool IsDiagnostic(SessionEvent ev) =>
        ev is SessionEvent.BadBody or SessionEvent.FrameResync or SessionEvent.DecoderFailed;

    public static DiagnosticsLevel Parse(string? spec) => spec?.ToLowerInvariant() switch
    {
        null or "" or "off" => Off,
        "on" => On,
        "summary" => Summary,
        _ => throw new FormatException($"--diagnostics must be off|on|summary, got '{spec}'")
    };

    public override bool Equals(object? obj) => obj is DiagnosticsLevel d && d.Name == Name;
    public override int GetHashCode() => Name.GetHashCode();
}
```

- [ ] **Step 4: Run, confirm 5 tests pass**

- [ ] **Step 5: Commit**

```
git add src/Mabipacade.Cli/Filters/DiagnosticsLevel.cs tests/Mabipacade.Cli.Tests/Filters/DiagnosticsLevelTests.cs
git commit -m "feat(cli): add DiagnosticsLevel for --diagnostics filtering"
```

---

## Phase 4 — Commands

### Task 10: ReplayCommand (pcap → stdout NDJSON)

The simpler subcommand; no live capture, no recording. Reads a pcap file, runs the pipeline, writes packets + events to stdout.

**Files:**
- Create: `src/Mabipacade.Cli/Commands/ReplayCommand.cs`
- Create: `src/Mabipacade.Cli/Logging/StderrLogger.cs`
- Modify: `src/Mabipacade.Cli/Program.cs` (wire subcommand)

- [ ] **Step 1: Write StderrLogger**

`src/Mabipacade.Cli/Logging/StderrLogger.cs`:
```csharp
namespace Mabipacade.Cli.Logging;

internal static class StderrLogger
{
    public static void Info(string msg) => Console.Error.WriteLine($"[info] {msg}");
    public static void Warn(string msg) => Console.Error.WriteLine($"[warn] {msg}");
    public static void Error(string msg) => Console.Error.WriteLine($"[error] {msg}");
}
```

- [ ] **Step 2: Implement ReplayCommand**

`src/Mabipacade.Cli/Commands/ReplayCommand.cs`:
```csharp
using System.CommandLine;
using Mabipacade.Cli.Filters;
using Mabipacade.Cli.Logging;
using Mabipacade.Cli.Output;
using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Sources;
using Mabipacade.Decoders;

namespace Mabipacade.Cli.Commands;

internal static class ReplayCommand
{
    public static Command Build()
    {
        var input = new Option<FileInfo>("--in") { Description = "Pcap file to replay", Required = true };
        var filterOp = new Option<string?>("--filter-op") { Description = "Comma-separated hex op list, e.g. 0x6984,0x7926" };
        var decodedOnly = new Option<bool>("--decoded-only") { Description = "Skip packets without an L3 decoder" };
        var diagnostics = new Option<string?>("--diagnostics") { Description = "off|on|summary (default off)" };

        var cmd = new Command("replay", "Replay a pcap file and emit NDJSON to stdout")
        {
            input, filterOp, decodedOnly, diagnostics
        };
        cmd.SetAction(parse => Run(
            parse.GetValue(input)!,
            parse.GetValue(filterOp),
            parse.GetValue(decodedOnly),
            DiagnosticsLevel.Parse(parse.GetValue(diagnostics))));
        return cmd;
    }

    private static int Run(FileInfo input, string? filterOpSpec, bool decodedOnly, DiagnosticsLevel diag)
    {
        if (!input.Exists)
        {
            StderrLogger.Error($"Input file does not exist: {input.FullName}");
            return 2;
        }

        OpFilter opFilter;
        try { opFilter = OpFilter.Parse(filterOpSpec); }
        catch (FormatException e) { StderrLogger.Error(e.Message); return 2; }

        var writer = new NdjsonWriter(Console.Out);
        using var source = new PcapFileFrameSource(input.FullName);
        var registry = new DecoderRegistry();
        DefaultDecoders.RegisterAll(registry);
        var pipeline = new PacketPipeline(source, registry);

        pipeline.PacketReceived += (_, p) =>
        {
            if (!opFilter.Allows(p.Op)) return;
            if (decodedOnly && p.Decoded is null) return;
            writer.WritePacket(p);
        };
        pipeline.SessionEventReceived += (_, ev) =>
        {
            if (!diag.PassesLive(ev)) return;
            writer.WriteEvent(ev);
        };

        StderrLogger.Info($"Replaying {input.Name}…");
        pipeline.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        // PcapFileFrameSource fires EndOfStream when the pcap is exhausted; StopAsync waits on its completion.
        pipeline.StopAsync().GetAwaiter().GetResult();
        StderrLogger.Info($"Done. Packets: {pipeline.Metrics.TotalPackets}, BadBody: {pipeline.Metrics.BadBodyCount}");
        return 0;
    }
}
```

- [ ] **Step 3: Wire into Program.cs**

Replace `src/Mabipacade.Cli/Program.cs`:
```csharp
using System.CommandLine;
using Mabipacade.Cli.Commands;

namespace Mabipacade.Cli;

public static class Program
{
    public static Task<int> Main(string[] args)
    {
        var root = new RootCommand("Mabipacade CLI — Mabinogi packet sidecar")
        {
            ReplayCommand.Build()
        };
        return root.Parse(args).InvokeAsync();
    }
}
```

- [ ] **Step 4: Build**

```
dotnet build
```
Expected: 0/0.

- [ ] **Step 5: Manually verify CLI**

```
dotnet run --project src/Mabipacade.Cli -- replay --help
```
Expected: prints help with `--in`, `--filter-op`, `--decoded-only`, `--diagnostics`.

- [ ] **Step 6: Commit**

```
git add src/Mabipacade.Cli/
git commit -m "feat(cli): add replay subcommand for pcap → stdout NDJSON"
```

---

### Task 11: CaptureCommand (live → stdout, with optional recording)

Reads from the live NIC selected by `GameEndpointResolver`. Writes packets + events to stdout. Optionally records to a session directory via `SessionRecorder`.

**Files:**
- Create: `src/Mabipacade.Cli/Commands/CaptureCommand.cs`
- Modify: `src/Mabipacade.Cli/Program.cs` (add subcommand)

- [ ] **Step 1: Implement CaptureCommand**

`src/Mabipacade.Cli/Commands/CaptureCommand.cs`:
```csharp
using System.CommandLine;
using Mabipacade.Cli.Filters;
using Mabipacade.Cli.Logging;
using Mabipacade.Cli.Output;
using Mabipacade.Cli.Recording;
using Mabipacade.Core.Capture;
using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Sources;
using Mabipacade.Decoders;
using PacketDotNet;
using SharpPcap;

namespace Mabipacade.Cli.Commands;

internal static class CaptureCommand
{
    public static Command Build()
    {
        var region = new Option<string>("--region") { Description = "Region profile: tw|jp|kr (default tw)", DefaultValueFactory = _ => "tw" };
        var process = new Option<string>("--process") { Description = "Process name to attach to (default Client.exe)", DefaultValueFactory = _ => "Client.exe" };
        var recordPcap = new Option<DirectoryInfo?>("--record-pcap") { Description = "Directory to record session files into" };
        var noStdout = new Option<bool>("--no-stdout") { Description = "Suppress stdout NDJSON (only record)" };
        var filterOp = new Option<string?>("--filter-op") { Description = "Comma-separated hex op list" };
        var decodedOnly = new Option<bool>("--decoded-only") { Description = "Skip packets without an L3 decoder" };
        var diagnostics = new Option<string?>("--diagnostics") { Description = "off|on|summary (default off)" };

        var cmd = new Command("capture", "Live-capture Mabinogi traffic and emit NDJSON to stdout")
        {
            region, process, recordPcap, noStdout, filterOp, decodedOnly, diagnostics
        };
        cmd.SetAction(parse => Run(
            parse.GetValue(region)!,
            parse.GetValue(process)!,
            parse.GetValue(recordPcap),
            parse.GetValue(noStdout),
            parse.GetValue(filterOp),
            parse.GetValue(decodedOnly),
            DiagnosticsLevel.Parse(parse.GetValue(diagnostics))));
        return cmd;
    }

    private static int Run(string regionName, string processName, DirectoryInfo? recordDir,
        bool noStdout, string? filterOpSpec, bool decodedOnly, DiagnosticsLevel diag)
    {
        OpFilter opFilter;
        try { opFilter = OpFilter.Parse(filterOpSpec); }
        catch (FormatException e) { StderrLogger.Error(e.Message); return 2; }

        var region = regionName switch
        {
            "tw" => RegionProfiles.Taiwan,
            "jp" => RegionProfiles.Japan,
            "kr" => RegionProfiles.Korea,
            _ => null,
        };
        if (region is null) { StderrLogger.Error($"Unknown region '{regionName}'"); return 2; }

        var procFinder = new ProcessFinder();
        var pid = procFinder.Find(Path.GetFileNameWithoutExtension(processName));
        if (pid is null)
        {
            StderrLogger.Warn($"{processName} not running; will fall back to region profile filter.");
        }

        var tcpTable = new Win32TcpConnectionTable();
        var resolver = new GameEndpointResolver(tcpTable, pid, region);
        var endpoint = resolver.TryResolveOnce();
        if (endpoint is null)
        {
            StderrLogger.Error("No game endpoint found. Is the game running and on a server you can reach?");
            return 3;
        }

        var nicSelector = new Win32NicSelector();
        var device = nicSelector.SelectFor(endpoint.RemoteAddress);
        if (device is null)
        {
            StderrLogger.Error($"No NIC routes to {endpoint.RemoteAddress}");
            return 3;
        }

        var bpf = $"tcp and src host {endpoint.RemoteAddress} and src port {endpoint.RemotePort}";
        StderrLogger.Info($"Capturing on {device.Description} with filter: {bpf}");

        using var source = new LiveFrameSource(device, bpf);
        var registry = new DecoderRegistry();
        DefaultDecoders.RegisterAll(registry);
        var pipeline = new PacketPipeline(source, registry);

        var writer = noStdout ? null : new NdjsonWriter(Console.Out);
        pipeline.PacketReceived += (_, p) =>
        {
            if (!opFilter.Allows(p.Op)) return;
            if (decodedOnly && p.Decoded is null) return;
            writer?.WritePacket(p);
        };
        pipeline.SessionEventReceived += (_, ev) =>
        {
            if (!diag.PassesLive(ev)) return;
            writer?.WriteEvent(ev);
        };

        SessionRecorder? recorder = null;
        if (recordDir is not null)
        {
            var sessionDir = Path.Combine(recordDir.FullName, DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss"));
            recorder = new SessionRecorder(sessionDir, regionName, pid, source, pipeline, LinkLayers.Ethernet);
            recorder.Start();
            StderrLogger.Info($"Recording to {sessionDir}");
        }

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        pipeline.StartAsync(cts.Token).GetAwaiter().GetResult();
        StderrLogger.Info("Press Ctrl+C to stop.");
        try { Task.Delay(Timeout.Infinite, cts.Token).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { }
        pipeline.StopAsync().GetAwaiter().GetResult();
        recorder?.Stop("UserStop");

        StderrLogger.Info($"Stopped. Packets: {pipeline.Metrics.TotalPackets}, BadBody: {pipeline.Metrics.BadBodyCount}");
        return 0;
    }
}
```

- [ ] **Step 2: Wire into Program.cs**

```csharp
using System.CommandLine;
using Mabipacade.Cli.Commands;

namespace Mabipacade.Cli;

public static class Program
{
    public static Task<int> Main(string[] args)
    {
        var root = new RootCommand("Mabipacade CLI — Mabinogi packet sidecar")
        {
            ReplayCommand.Build(),
            CaptureCommand.Build()
        };
        return root.Parse(args).InvokeAsync();
    }
}
```

- [ ] **Step 3: Build + verify**

```
dotnet build
dotnet run --project src/Mabipacade.Cli -- capture --help
```
Expected: 0/0; help includes `--region`, `--process`, `--record-pcap`, `--no-stdout`, etc.

- [ ] **Step 4: Commit**

```
git add src/Mabipacade.Cli/Commands/CaptureCommand.cs src/Mabipacade.Cli/Program.cs
git commit -m "feat(cli): add capture subcommand for live → stdout NDJSON + recording"
```

---

## Phase 5 — End-to-end + contract tests

### Task 12: Replay command E2E (subprocess)

Spawns `mabipacade replay` as a subprocess against the existing pcap fixture, parses stdout, validates ≥1 packet line.

**Files:**
- Create: `tests/Mabipacade.Cli.Tests/Commands/ReplayCommandTests.cs`

- [ ] **Step 1: Write the test**

`tests/Mabipacade.Cli.Tests/Commands/ReplayCommandTests.cs`:
```csharp
using System.Diagnostics;
using System.Text.Json;

namespace Mabipacade.Cli.Tests.Commands;

public class ReplayCommandTests
{
    private static readonly string FixturePath = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..",
        "Mabipacade.Core.Tests", "fixtures", "known_good.pcap");

    private static string CliProjectPath => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "src", "Mabipacade.Cli", "Mabipacade.Cli.csproj"));

    [Fact(Skip = "Requires local fixture — copy a pcap to tests/Mabipacade.Core.Tests/fixtures/known_good.pcap")]
    public void Replay_EmitsValidNdjsonLines_ForRealPcap()
    {
        if (!File.Exists(FixturePath)) return;

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{CliProjectPath}\" -- replay --in \"{FixturePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var proc = Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(60_000);

        Assert.Equal(0, proc.ExitCode);

        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length > 0, $"expected at least one NDJSON line. stderr:\n{stderr}");

        int packetCount = 0;
        foreach (var line in lines)
        {
            var doc = JsonDocument.Parse(line);
            Assert.True(doc.RootElement.TryGetProperty("kind", out var kind));
            var k = kind.GetString();
            Assert.True(k == "packet" || k == "event", $"unexpected kind '{k}'");
            if (k == "packet")
            {
                packetCount++;
                Assert.True(doc.RootElement.TryGetProperty("op", out _));
                Assert.True(doc.RootElement.TryGetProperty("elems", out _));
            }
        }
        Assert.True(packetCount > 0, "expected at least one packet line");
    }
}
```

- [ ] **Step 2: Run, confirm Skip respected**

```
dotnet test tests/Mabipacade.Cli.Tests
```
Expected: 1 skipped test.

- [ ] **Step 3: Verify locally with fixture**

Engineer copies `known_good.pcap` to `tests/Mabipacade.Core.Tests/fixtures/` (gitignored), removes Skip locally, runs:
```
dotnet test tests/Mabipacade.Cli.Tests --filter "FullyQualifiedName~ReplayCommandTests"
```
Expected: passes with >0 packet lines. Re-add Skip before commit so CI stays green without fixture.

- [ ] **Step 4: Commit**

```
git add tests/Mabipacade.Cli.Tests/Commands/ReplayCommandTests.cs
git commit -m "test(cli): add replay subprocess E2E (fixture-skipped)"
```

---

### Task 13: NDJSON contract tests (in-process, no fixture)

Runs synthetic data through `NdjsonWriter` and validates each output line is well-formed JSON with the required keys. This is the schema contract for cross-language consumers — failing it means breaking mabidilmeter and friends.

**Files:**
- Create: `tests/Mabipacade.Cli.Tests/Schema/NdjsonContractTests.cs`

- [ ] **Step 1: Write the test**

```csharp
using System.Text;
using System.Text.Json;
using Mabipacade.Cli.Output;
using Mabipacade.Core.Diagnostics;
using Mabipacade.Core.Model;
using System.Net;

namespace Mabipacade.Cli.Tests.Schema;

public class NdjsonContractTests
{
    private static string RenderPackets(params MabiPacket[] packets)
    {
        var sb = new StringBuilder();
        using var sw = new StringWriter(sb);
        var writer = new NdjsonWriter(sw);
        foreach (var p in packets) writer.WritePacket(p);
        return sb.ToString();
    }

    [Fact]
    public void Packet_HasRequiredFields()
    {
        var output = RenderPackets(new MabiPacket(
            new DateTime(2026, 5, 13, 8, 0, 0, DateTimeKind.Utc),
            Direction.Inbound, 0x6984, 12345UL,
            new[] { MessageElem.Short(59000) }, null));
        var line = output.TrimEnd();
        var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        foreach (var required in new[] { "kind", "ts", "dir", "op", "opName", "entityId", "type", "decoded", "elems" })
            Assert.True(root.TryGetProperty(required, out _), $"missing required field: {required}");

        Assert.Equal("packet", root.GetProperty("kind").GetString());
        Assert.Equal("in", root.GetProperty("dir").GetString());
        Assert.Equal("0x6984", root.GetProperty("op").GetString());
        Assert.Equal("12345", root.GetProperty("entityId").GetString());      // uint64 as string
    }

    [Fact]
    public void Event_HasRequiredFields()
    {
        var sb = new StringBuilder();
        using var sw = new StringWriter(sb);
        var writer = new NdjsonWriter(sw);
        writer.WriteEvent(new SessionEvent.ConnectionResumed(DateTime.UtcNow,
            new IPEndPoint(IPAddress.Parse("1.2.3.4"), 11000), SameAsLast: false));
        var line = sb.ToString().TrimEnd();
        var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        foreach (var required in new[] { "kind", "ts", "type", "newRemote", "sameAsLast" })
            Assert.True(root.TryGetProperty(required, out _), $"missing required field: {required}");

        Assert.Equal("event", root.GetProperty("kind").GetString());
        Assert.Equal("ConnectionResumed", root.GetProperty("type").GetString());
        Assert.False(root.GetProperty("sameAsLast").GetBoolean());
    }

    [Fact]
    public void Elem_UsesTV_Compact()
    {
        var output = RenderPackets(new MabiPacket(
            DateTime.UtcNow, Direction.Inbound, 0x6984, 0UL,
            new[] { MessageElem.Short(42) }, null));
        var doc = JsonDocument.Parse(output.TrimEnd());
        var elem = doc.RootElement.GetProperty("elems")[0];
        Assert.True(elem.TryGetProperty("t", out _));
        Assert.True(elem.TryGetProperty("v", out _));
    }

    [Fact]
    public void UnknownOp_OpNameIsNull_NotMissing()
    {
        var output = RenderPackets(new MabiPacket(
            DateTime.UtcNow, Direction.Inbound, 0xFFFF, 0UL,
            Array.Empty<MessageElem>(), null));
        var doc = JsonDocument.Parse(output.TrimEnd());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("opName").ValueKind);
    }
}
```

- [ ] **Step 2: Run, confirm 4 tests pass**

- [ ] **Step 3: Commit**

```
git add tests/Mabipacade.Cli.Tests/Schema/NdjsonContractTests.cs
git commit -m "test(cli): add NDJSON schema contract tests for cross-language consumers"
```

---

## Phase 6 — Final green

### Task 14: Full test suite green + manual verification

- [ ] **Step 1: Run all tests**

```
dotnet test
```
Expected: all pass, 3 skipped allowed (2 fixture-dependent from M1 + 1 new replay E2E).

- [ ] **Step 2: Verify build clean**

```
dotnet build /warnaserror
```
Expected: 0/0.

- [ ] **Step 3: Manual smoke test — replay against fixture**

Engineer locally:
```
copy tests/Mabipacade.Core.Tests/fixtures/known_good.pcap to a known place (or use existing)
dotnet run --project src/Mabipacade.Cli -- replay --in tests/Mabipacade.Core.Tests/fixtures/known_good.pcap
```
Expected: stdout = multiple lines of NDJSON, each parseable JSON with `kind` field. stderr = `[info]` log lines for start/finish.

Spot-check three of the lines manually — confirm they match the schema in §3 of the spec.

- [ ] **Step 4: Manual smoke test — op filter**

```
dotnet run --project src/Mabipacade.Cli -- replay --in <fixture> --filter-op 0x526C
```
Expected: only packet lines for `op=0x526C` appear (Chat). Event lines still appear.

- [ ] **Step 5: Final commit (only if cleanup needed)**

```
git status
# if anything is stray, fix and:
git add -A
git commit -m "chore(cli): M2 final clean-up"
```

---

## Done conditions for M2

When this plan finishes, the following must hold:

- [x] `dotnet build /warnaserror` clean
- [x] `dotnet test` green (skipped fixture tests OK)
- [x] `mabipacade replay --in <pcap>` emits valid NDJSON to stdout
- [x] `mabipacade capture --region tw --record-pcap <dir>` runs live capture and writes session files (manual verification only)
- [x] stdout / stderr strictly separated (NDJSON / log)
- [x] `kind` discriminator on every line, `t`/`v` compact elem shape, uint64 as string, op as `0xXXXX`
- [x] `--filter-op`, `--decoded-only`, `--diagnostics` all functional
- [x] Cross-language consumer can do `subprocess + read stdout line by line` and get usable data — verified by `NdjsonContractTests`

The deliverable enables a Go program like `mabidilmeter-mogu-2` to consume Mabinogi packets like this:

```go
cmd := exec.Command("mabipacade.exe", "capture", "--region", "tw", "--filter-op", "0x7926,0x6984")
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
        // handle connection lost/resumed/etc.
    }
}
```

---

## Open follow-ups for M3+

- `--diagnostics summary` currently passes through like `on`. Real aggregation (rate-limited summary lines) lands when DebugUi needs it.
- `--rate`, `--start-at`, `--end-at` for `replay` were in the spec but defer to M3 (they need `ReplayTransport` wiring; M2 just plays the pcap at full speed).
- The capture subcommand's reconnect handling assumes `LiveFrameSource` keeps its NIC handle across endpoint changes. `CaptureSession.RunAsync` is not yet wired into the command — reconnects to a different endpoint require process restart for now. Wiring this in is M3 territory.
- Schema versioning: NDJSON has no `v: 1` field today. If we ever need to evolve the shape, add a top-level version field per envelope. Defer until first real schema break.
