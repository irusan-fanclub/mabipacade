# Mabipacade

Mabinogi 網路封包解析器 — C# library + CLI sidecar + WebSocket server + WPF debug GUI。

從 NIC 抓 Mabinogi 的 server → client TCP 流量、解開外層 framing、Message body、再餵給 24 個 op 級別的 decoder 還原成強型別 POCO。

跑在 Windows，靠 Npcap 抓封包。

```
        ┌──────────────┐
        │ Mabinogi 客  │
        │  端 (Win)    │
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
       MabiPacket    SessionEvent   PcapWriter
           │            │            │
   ┌───────┼────────────┼─────────┐  └─→ session.pcap
   │       │            │         │
   ▼       ▼            ▼         ▼
  Cli   Server      DebugUi      其他 .NET 消費者
 NDJSON  ws://      WPF GUI       (events 訂閱)
  to     :9876
 stdout
```

---

## 適用對象

| 角色 | 用什麼 |
|---|---|
| 寫 damage meter / boss notifier 的 **.NET 開發者** | `Mabipacade.Core` + `Mabipacade.Decoders`（events API） |
| **Go / Python / JS** 工具想訂封包流 | `Mabipacade.Cli`（subprocess + stdout NDJSON） |
| **瀏覽器前端** 或多消費者場景 | `Mabipacade.Server`（WebSocket） |
| 想看封包流、抓 bug、查 op 沒解到 | `Mabipacade.DebugUi`（WPF live + replay） |

---

## 系統需求

- **Windows**（Mabinogi 只支援 Windows，所以這個 toolchain 沒打算跨平台）
- **.NET 10 SDK**
- **[Npcap](https://npcap.com/)** — 抓封包的 native lib，安裝時請勾 *"Install Npcap in WinPcap API-compatible Mode"*
- pcap 重放模式不需要 Npcap 運行時，但 SharpPcap 啟動仍會嘗試載入 — 建議仍裝

---

## Quick Start

### 1. WPF GUI（最完整體驗）

```powershell
dotnet run --project src/Mabipacade.DebugUi
```

開窗後：
- 點 **Open pcap…** 選一個 pcap → ▶ 開始 replay（預設 Max 速度全速載入；要視覺重播選 1x / 0.5x）
- 點 **Start Live** → 自動找 `Client.exe`、resolve 遊戲伺服器 endpoint、開 Npcap 抓
- 中間 DataGrid 顯示 packet 列、右側 tabs 看 Decoded JSON / Elems / Hex / Names
- 上方 status bar 顯示 ● Live / ▶ Replay / ○ Stopped
- 名稱解析：編輯 `settings.json`（跟 `.exe` 同層）加 `"xmlDataDirectory": "..."`。支援兩種 layout：
  - **Flat** — `SkillInfo.xml` + `SkillInfo.taiwan.txt` 直接放在指定資料夾
  - **解包後** — 指 `.it` 解出來的根目錄；loader 會自動到 `data/db/Skill/SkillInfo.xml` + `data/local/xml/SkillInfo.taiwan.txt` 找

### 2. CLI sidecar（Go / Python / JS 消費）

```powershell
# Replay pcap
mabipacade replay --in tests/Mabipacade.Core.Tests/fixtures/known_good.pcap

# Live capture
mabipacade capture --region tw --filter-op 0x7926,0x6984

# Capture + record session
mabipacade capture --record-pcap ./sessions
```

stdout 一行一筆 JSON（NDJSON），stderr 是 log。Go 端範例：

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

瀏覽器：

```javascript
const ws = new WebSocket('ws://127.0.0.1:9876')
ws.onmessage = e => console.log(JSON.parse(e.data))
// 訂閱 filter
ws.send('{"op":"subscribe","kinds":["packet"],"ops":["0x7926","0x6984"]}')
```

### 4. C# Library

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
        Console.WriteLine("換伺服器了，要 reset 狀態");
};

await pipeline.StartAsync(CancellationToken.None);
```

---

## NDJSON Schema

stdout / WebSocket 同一份 schema。每行一個 JSON object，`kind` 是 discriminator。

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

關鍵欄位：

| Field | Notes |
|---|---|
| `op` | 永遠 hex string `"0xXXXX"` — 不要用 decimal（會跟 enum 值對不上）|
| `entityId` | uint64 用 **string**（JS number 超過 2^53 會掉精度）|
| `opName` | OpCodes enum 對得到才有、否則 `null` |
| `type` | L3 decoder POCO 類別名（如 `PlayerSkillPrepareStart`）；沒解到就 `null` |
| `decoded` | POCO 序列化、camelCase；沒 decoder 時 `null` |
| `elems` | L2 elem list **永遠有**；`t` = 1..7 型別字串，`v` = 內容（uint64 也用 string）|
| `dir` | `"in"` / `"out"` — 目前 v1 永遠 `"in"`，消費者請別假設 |

### Event

```json
{"kind":"event","ts":"...","type":"SessionStart","region":"tw","processId":4812}
{"kind":"event","ts":"...","type":"ConnectionLost","lastRemote":"61.218.1.2:11000"}
{"kind":"event","ts":"...","type":"ConnectionResumed","newRemote":"61.218.1.3:11000","sameAsLast":false}
{"kind":"event","ts":"...","type":"BadBody","op":"0x9093","length":42}
```

8 種 type（`SessionStart` / `SessionEnd` / `ConnectionEstablished` / `ConnectionLost` / `ConnectionResumed` / `FrameResync` / `BadBody` / `DecoderFailed`）。

**`SameAsLast`**：`true` = 短暫斷線同一 endpoint，狀態通常可以保留；`false` = 換伺服器/頻道，狀態應該 reset。

### Session 錄製檔

`mabipacade capture --record-pcap <dir>` 跟 `Mabipacade.DebugUi` 都產這三個 sidecar 檔：

```
sessions/
└── 2026-05-13T18-23-11/
    ├── session.pcap            # 原始封包 (Wireshark 可開)
    ├── session.events.ndjson   # SessionEvent 流
    └── session.json            # session metadata + stats
```

---

## 內建 24 個 Decoder

從 `D:/Projects/Notes/mabinogi-packet-decoding/README.md` 整理出來的已驗證 op：

| Category | Ops | POCO |
|---|---|---|
| Combat | `0x7924` `0x7925` `0x7926` | `CombatAction`, `CombatActionEnd`, `CombatActionPack(AttackerId, Sub[])` |
| Skills | `0x6984` `0x6985` `0x6988` `0x6989` `0x698B` `0x6993` | `PlayerSkillPrepare*`, `PlayerSkillPostCastAck*`, `PlayerSkillStop` |
| Entity | `0x520C` `0x520D` `0x5334` `0x5335` `0x53FC` | `EntityAppear(RaceId, Name)`, `EntityDisappear`, `EntitiesAppear/Disappear`, `IsNowDead` |
| Stats | `0x7530` `0x7532` `0x7534` `0xA028` | marker types (body shape 待驗證) |
| Misc | `0x526C` `0x9091` `0x9095` `0xA41E` `0xA43C` `0x59E6` | `Chat(Sender, Message)`, `Effect`, `SharpMind`, `EquipmentChanged`... |

⚠️ 多數 marker 還沒解 body shape — 看到 packet 但 decoded 是 empty POCO 是正常的。

### 加自己的 decoder

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
DefaultDecoders.RegisterAll(registry);   // 24 個內建
registry.Register(new MyDecoder());      // 自己加
```

---

## 專案結構

```
mabipacade/
├── src/
│   ├── Mabipacade.Core/         # Pipeline、capture、recording、replay、JSON
│   ├── Mabipacade.Decoders/     # 24 個 L3 decoder + OpCodes + OpCodeNames
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
│       ├── specs/                     # 設計規格
│       └── plans/                     # 各 milestone 實作 plan
└── mabipacade.sln
```

**Tech stack：** .NET 10 · C# 13 · SharpPcap 6.3.1 · PacketDotNet 1.4.8 · System.CommandLine 2.0.0-beta5 · Fleck 1.2 · WPF · xUnit

---

## Pipeline Stage 圖

```
┌──────────────────────────────────────────────────────────────────────┐
│  Stage 0  Source        IFrameSource (live / pcap 共用 abstraction)   │
│  Stage 1  L2/L3/L4      PacketDotNet → TcpFrame                       │
│  Stage 2  Direction     BPF `src host` filter — server → client only  │
│  Stage 3  Reassembly    Per-5-tuple TcpReassembler (seq order + dup)  │
│  Stage 4  Framing       MabiPacketFramer (sign+length+flag+op+id+body)│
│  Stage 5  L2 decode     MessageElemReader (uvarint count, BE elems)   │
│  Stage 6  L3 plugin     DecoderRegistry.TryGet(op) → IPacketDecoder   │
│  Stage 7  Fan-out       PacketReceived event + PcapWriter + sinks     │
└──────────────────────────────────────────────────────────────────────┘
```

failure modes：
- Stage 4 `FramingError` → reassembler `Reset()`、emit `SessionEvent.FrameResync`
- Stage 5 `BadBody`（tag 不在 1..7、長度截掉、count overflow）→ 整包跳、emit `SessionEvent.BadBody`
- Stage 6 decoder throw → 降級成 L2 emit、emit `SessionEvent.DecoderFailed`
- Stage 7 sink 過慢 → drop 最舊 + emit `SessionEvent.SinkOverflow`（不阻塞 pipeline）

---

## Wire format 速查

外殼 6 bytes：

| Offset | Size | Field | Encoding |
|---|---|---|---|
| 0 | 1 | sign | byte (unused) |
| 1 | 4 | length | uint32 **LE**（含 6-byte header）|
| 5 | 1 | flag | byte（0/3/4 = normal，1/2 = short heartbeat，>4 = framing error）|

Body of a normal packet (`body = bytes[6..length]`)：

| Offset | Size | Field | Encoding |
|---|---|---|---|
| 0 | 4 | op | uint32 **BE**（top 16 bits 0，cast 到 ushort）|
| 4 | 8 | entityId | uint64 **BE** |
| 12 | varies | reserved uvarint + Message body | |

Message body：`[outer reserved uvarint][count uvarint][reserved 0 byte][elem * N]`

Elem：`[tag:1][value]`，tag ∈ 1..7：

| Tag | Type | Value |
|---|---|---|
| 1 | Byte | 1 byte |
| 2 | Short | uint16 **BE** |
| 3 | Int | uint32 **BE** |
| 4 | Long | uint64 **BE** |
| 5 | Float | float32 **LE**（唯一 LE 欄位）|
| 6 | String | uint16 **BE** length（含尾 NUL）+ UTF-8 bytes |
| 7 | Bin | uint16 **BE** length + raw bytes |

---

## Build / Test

```powershell
dotnet build -p:TreatWarningsAsErrors=true
dotnet test
```

預期 **201 pass + 4 skip**（skipped 是 fixture/環境相依測試）。

要跑 E2E pcap 測試先把一個 pcap 放到：
```
tests/Mabipacade.Core.Tests/fixtures/known_good.pcap
```
（gitignored），然後拿掉測試上的 `[Fact(Skip=...)]`。

---

## 限制 / Not-yet

- **韓服**封包加密、需要 iptime router 中繼 — 不支援
- **Outbound** 方向（client → server）— 預留 `Direction.Outbound` 跟 NDJSON `dir`，沒實作
- **跨網卡漫遊**（VPN / 虛擬交換器）— `GameEndpointResolver` 不會自動切換，要重啟
- **`--rate` / `--start-at` / `--end-at`** flag — Cli 沒接 `ReplayTransport`，只 GUI 有
- **`--diagnostics summary`** — 解析但未實作（fallback 到 `on`），M3 deferred
- **多數 marker decoder body shape** 未驗證 — 看到 packet 但 decoded 內容空是正常的
- **`--rate` 高速 + 大量 packet** 時 GUI 不做批次更新、ObservableCollection 逐筆 add 會卡（M3.5+ 改善）

---

## 設計文件

- **Design spec** — [`docs/superpowers/specs/2026-05-13-mabipacade-design.md`](docs/superpowers/specs/2026-05-13-mabipacade-design.md)
- **Implementation plans** — `docs/superpowers/plans/`（M1 Core+Decoders、M2 Cli、M3 DebugUi）
- **參考筆記** — `D:/Projects/Notes/mabinogi-packet-decoding/README.md`（wire format、opcode 表、踩過的雷）
