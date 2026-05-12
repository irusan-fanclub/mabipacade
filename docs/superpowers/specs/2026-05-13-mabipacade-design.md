# Mabipacade — Mabinogi 封包解析器設計規格

**日期**：2026-05-13
**狀態**：Design approved，待轉 implementation plan

## 目的

`Mabipacade` 是一個 **C# 模組與工具集**，負責即時擷取並解析 Mabinogi 的網路封包，把結果用 **C# events**（給 .NET 消費者）與 **NDJSON stream**（給 Go / Python / JS 等跨語言消費者）兩種介面交付。

主要消費者場景：

- 自己的 C# 工具（damage meter、boss notifier、replay viewer）直接吃 events
- 其他語言寫的工具（例如 [`mabidilmeter-mogu-2`](https://github.com/irusan-fanclub/mabidilmeter)，Go + Vite）透過 subprocess + stdout NDJSON 訂閱
- 未來瀏覽器前端透過 WebSocket 直連

## 非目標（明確 out of scope）

- **韓服**封包加密處理（v1 不支援）
- **Outbound** 方向（client → server）擷取——型別與 NDJSON `dir` 欄位預留，實作延後
- **Name mapping**（SkillId → 技能名）**不在 library / CLI / NDJSON 輸出**，只在 DebugUi 做為 view-side enrichment
- **mabi-pack2 整合**——library 不 spawn mabi-pack2；DebugUi 名稱解析時讀使用者預先解好的 XML

## 架構總覽

### Solution layout

```
mabipacade/
├── src/
│   ├── Mabipacade.Core/         # Library — pipeline、capture、recording、replay
│   ├── Mabipacade.Decoders/     # 內建 L3 decoder（CombatActionPack、PlayerSkill*…）
│   ├── Mabipacade.Cli/          # Console exe — stdout NDJSON sidecar
│   ├── Mabipacade.Server/       # Console exe — WebSocket server（M4）
│   └── Mabipacade.DebugUi/      # WPF exe — live + replay GUI
├── tests/
│   ├── Mabipacade.Core.Tests/
│   ├── Mabipacade.Decoders.Tests/
│   └── Mabipacade.Cli.Tests/
├── docs/
│   └── superpowers/specs/
├── mabipacade.sln
└── README.md
```

### Project 角色與依賴

| Project | Output | 相依 |
|---|---|---|
| `Mabipacade.Core` | Class Library | `SharpPcap` 6.3.1 `PacketDotNet` 1.4.8 |
| `Mabipacade.Decoders` | Class Library | `Mabipacade.Core` |
| `Mabipacade.Cli` | Console exe | `Core` + `Decoders` + `System.CommandLine` |
| `Mabipacade.Server` | Console exe | `Core` + `Decoders` + `Fleck` |
| `Mabipacade.DebugUi` | WPF exe | `Core` + `Decoders` + WPF |

**Tech baseline**：.NET 10，跟既有 `mabi_stage4_boss_notifier` 一致。Windows only（Mabinogi 本身只 Windows，跨平台不加分）。

**Dependency 方向**：所有非 Core project 引用 Core + Decoders 並向 Core 註冊 plugin。沒有循環依賴。

### 交付里程碑

| 階段 | 交付 | 對外可用情境 |
|---|---|---|
| M1 | `Core` + `Decoders` + tests | C# 消費者直接吃 events |
| M2 | `Cli` (NDJSON stdout) | 跨語言消費者（Go / Python / JS）|
| M3 | `DebugUi`（live + replay） | 自己看封包、dogfood 驗證 |
| M3.5 | DebugUi 名稱解析（view-side） | 顯示「技能名」而非 SkillId |
| M4 | `Server`（WebSocket） | 多消費者、瀏覽器前端 |

---

## §1 Packet 解析 pipeline

### Stage 圖

```
┌──────────────────────────────────────────────────────────────────────┐
│  Stage 0 — Source                                                    │
│    Live:   SharpPcap CaptureDevice.OnPacketArrival                   │
│    Replay: SharpPcap CaptureFileReaderDevice                         │
│    ↓ RawFrame { byte[] Data, LinkLayerType, Timeval }                │
├──────────────────────────────────────────────────────────────────────┤
│  Stage 1 — L2/L3/L4 dissect (PacketDotNet)                           │
│    Packet.ParsePacket(linkLayer, data).Extract<TcpPacket>()          │
│    ↓ TcpFrame { srcIp, srcPort, dstIp, dstPort, seq, payload, ts }   │
├──────────────────────────────────────────────────────────────────────┤
│  Stage 2 — Direction & endpoint filter                               │
│    BPF kernel-level: `tcp and src host <gameIp>` → 只收 inbound      │
│    ↓                                                                  │
├──────────────────────────────────────────────────────────────────────┤
│  Stage 3 — TCP stream reassembly                                     │
│    per (srcIp, srcPort, dstIp, dstPort) maintain buffer              │
│    order by seq, drop dup                                            │
│    ↓ ordered byte stream                                             │
├──────────────────────────────────────────────────────────────────────┤
│  Stage 4 — Game packet framing                                       │
│    [4-byte length][1-byte flag][op header][8-byte entity id][body]   │
│    framing error → realign（drop oldest segment, resync）            │
│    ↓ MabiPacketSlice { op, entityId, bodyBytes, ts }                 │
├──────────────────────────────────────────────────────────────────────┤
│  Stage 5 — Message body decode (L2)                                  │
│    walk elem list; each elem = [type tag][value]                     │
│    type tag must be 1..7; otherwise → BadBody                        │
│    ↓ (op, entityId, elems[], ts)                                     │
├──────────────────────────────────────────────────────────────────────┤
│  Stage 6 — L3 decode (plugin)                                        │
│    lookup decoder by op; if found → run, attach typed                │
│    ↓ MabiPacket { ts, dir, op, entityId, elems[], decoded? }         │
├──────────────────────────────────────────────────────────────────────┤
│  Stage 7 — Fan-out                                                   │
│    → C# events  (PacketReceived / SessionEventReceived)              │
│    → pcap writer  (Stage 0 分支 raw frame)                            │
│    → NDJSON writer (Cli)                                             │
│    → WebSocket broadcast (Server)                                    │
│    → GUI ObservableCollection (DebugUi)                              │
└──────────────────────────────────────────────────────────────────────┘
```

### Key invariants

- **Live 與 Replay 共用同一條 pipeline**，差別只在 Stage 0 source。
- **Pcap 錄製從 Stage 0 分支**（記 raw frame，**不**記解析結果）——這樣將來 decoder 修了，舊 pcap 還能 reprocess。
- **Timestamp 一律 UTC + ISO 8601**——避免筆記裡 pcap UTC vs C# local 對不上的雷。
- **Sink 之間用 `System.Threading.Channels.Channel<T>`**，慢 sink 不阻塞 pipeline；channel 滿時 drop 最舊 + emit `SinkOverflowEvent`。

### Stage 4 三狀態（沿用既有專案 ParseStatus）

| 狀態 | 處理 |
|---|---|
| `Ok` | 進 Stage 5 |
| `BadBody` | 整 packet skip，buffer 指標前進（外殼長度可信）|
| `FramingError` | buffer 從最舊 TCP segment 重組（不汙染後續 packet）|

---

## §2 模組邊界

### `Mabipacade.Core`

```
Mabipacade.Core/
├── Sources/
│   ├── IFrameSource.cs              # Stage 0 抽象
│   ├── LiveFrameSource.cs
│   └── PcapFileFrameSource.cs       # 支援 pause / step / rate
├── Capture/
│   ├── ProcessFinder.cs             # 找 Client.exe → PID
│   ├── TcpConnectionTable.cs        # GetExtendedTcpTable wrapper
│   ├── GameEndpointResolver.cs      # PID → endpoint；含 fallback
│   ├── RegionProfile.cs             # 退路：寫死的 IP/port 範圍
│   └── NicSelector.cs               # endpoint → NIC（GetBestInterface）
├── Pipeline/
│   ├── TcpReassembler.cs            # Stage 3
│   ├── MabiPacketFramer.cs          # Stage 4
│   ├── MessageElemReader.cs         # Stage 5
│   ├── DecoderRegistry.cs           # Stage 6 plugin map
│   └── PacketPipeline.cs            # Facade，串起 Stage 0-7
├── Model/
│   ├── MabiPacket.cs                # 公開：pipeline 終端輸出
│   ├── MessageElem.cs               # 公開
│   ├── Direction.cs                 # enum {Inbound, Outbound}
│   ├── RawFrame.cs                  # internal
│   ├── TcpFrame.cs                  # internal
│   └── MabiPacketSlice.cs           # internal
├── Recording/
│   └── PcapWriter.cs                # Stage 0 分支
├── Replay/
│   └── ReplayTransport.cs           # play/pause/step/rate/seekForward
├── Plugins/
│   └── IPacketDecoder.cs            # L3 decoder 介面
├── Diagnostics/
│   ├── SessionEvent.cs              # SessionStart/End、ConnectionLost/Resumed 等
│   └── PipelineMetrics.cs           # pps、bps、buffer 長度
└── Time/
    └── Timestamp.cs                 # 強制 UTC + ISO 8601
```

**對外 Facade**：`PacketPipeline`。其他型別大部分 internal。

### `Mabipacade.Decoders`

一個 op 一檔，**POCO 與 decoder 同檔**。

```
Mabipacade.Decoders/
├── OpCodes.cs                       # enum，全 hex literal
├── Combat/
│   ├── CombatActionDecoder.cs       # 0x7924
│   ├── CombatActionEndDecoder.cs    # 0x7925
│   └── CombatActionPackDecoder.cs   # 0x7926（含 sub-packet）
├── Skills/
│   ├── PlayerSkillPrepareStartDecoder.cs    # 0x6984
│   ├── PlayerSkillPrepareProgressDecoder.cs # 0x6993（SkillId 在 msg[2]）
│   ├── PlayerSkillPrepareReadyDecoder.cs    # 0x6985
│   ├── PlayerSkillPostCastAck1Decoder.cs    # 0x6988
│   ├── PlayerSkillPostCastAck2Decoder.cs    # 0x6989
│   └── PlayerSkillStopDecoder.cs            # 0x698B（無 SkillId）
├── Entity/
│   ├── EntityAppearDecoder.cs       # 0x520C
│   ├── EntityDisappearDecoder.cs    # 0x520D
│   ├── EntitiesAppearDecoder.cs     # 0x5334
│   ├── EntitiesDisappearDecoder.cs  # 0x5335
│   └── IsNowDeadDecoder.cs          # 0x53FC
├── Stats/
│   ├── StatUpdatePrivateDecoder.cs  # 0x7530
│   ├── StatUpdatePublicDecoder.cs   # 0x7532
│   ├── EntityRelatedDecoder.cs      # 0x7534
│   └── ConditionUpdate2Decoder.cs   # 0xA028
├── Misc/
│   ├── ChatDecoder.cs               # 0x526C
│   ├── EffectDecoder.cs             # 0x9091
│   ├── EffectDelayedDecoder.cs      # 0x9095
│   ├── SharpMindDecoder.cs          # 0xA41E
│   ├── PartyWindowUpdateDecoder.cs  # 0xA43C
│   └── EquipmentChangedDecoder.cs   # 0x59E6
└── DefaultDecoders.cs               # 一鍵全註冊 helper
```

**初始覆蓋率**：跟筆記表全做（24 op）。

### `Mabipacade.Cli`

```
Mabipacade.Cli/
├── Program.cs                       # System.CommandLine
├── Commands/
│   ├── CaptureCommand.cs            # live → stdout NDJSON
│   └── ReplayCommand.cs             # pcap → stdout NDJSON
├── Output/
│   ├── NdjsonWriter.cs              # MabiPacket / SessionEvent → 一行 JSON
│   └── PacketJsonShape.cs           # 序列化形狀定義
└── Logging/
    └── StderrLogger.cs              # stderr 走 log，stdout 純 NDJSON
```

### `Mabipacade.Server`（M4）

```
Mabipacade.Server/
├── Program.cs
├── WebSocketHost.cs                 # Fleck，綁 127.0.0.1
├── Subscriptions.cs                 # 多 client、可 filter (kinds + ops)
└── Envelope.cs                      # WS message 包裝
```

### `Mabipacade.DebugUi`（M3）

```
Mabipacade.DebugUi/
├── App.xaml(.cs)                    # 啟動 Core pipeline
├── MainWindow.xaml(.cs)             # Live / Replay tab
├── ViewModels/
│   ├── MainViewModel.cs
│   ├── SourceViewModel.cs           # Live or Replay 切換
│   ├── FilterViewModel.cs           # op / entity / decodedOnly
│   ├── PacketListViewModel.cs       # virtualized ObservableCollection
│   ├── PacketDetailViewModel.cs
│   ├── ReplayTransportViewModel.cs
│   └── StatusViewModel.cs
├── Controls/
│   ├── HexDumpView.xaml
│   ├── ElemTreeView.xaml
│   └── ReplayTransport.xaml         # view of ReplayTransport controller
├── NameResolution/                  # M3.5 後段加上
│   ├── SkillNameMap.cs              # SkillInfo.xml + .taiwan.txt
│   ├── ConditionNameMap.cs
│   └── NameResolver.cs              # 純 view-side enrichment
└── Settings/
    └── Persistence.cs               # 寫到 APPDIR（AppContext.BaseDirectory）
```

---

## §3 公開 API 與消費者契約

### `MabiPacket` 是唯一對外輸出形狀

```csharp
namespace Mabipacade.Core;

public sealed record MabiPacket(
    DateTime TimestampUtc,
    Direction Direction,                    // 目前永遠 Inbound
    ushort Op,
    ulong EntityId,
    IReadOnlyList<MessageElem> Elems,       // L2，永遠有
    object? Decoded                         // L3 POCO，沒 decoder = null
);

public enum Direction { Inbound, Outbound }
```

**契約**：`Elems` 永遠有；`Decoded` 是 bonus。消費者沒 L3 時可用 Elems 自己處理。

### Pipeline events

```csharp
public sealed class PacketPipeline {
    public event EventHandler<MabiPacket> PacketReceived;
    public event EventHandler<SessionEvent> SessionEventReceived;
    // 兩 event 在同一 pipeline thread 觸發 → 時序自動維持

    public Task StartAsync(CancellationToken ct);
    public Task StopAsync();
}
```

### `SessionEvent` hierarchy

```csharp
public abstract record SessionEvent(DateTime TimestampUtc) {
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

**`SameAsLast` 旗標**：
- `true` = 同 IP:Port 短暫重連，消費者可選擇不 reset
- `false` = 換伺服/換頻道，狀態應 reset

### L3 plugin 介面

`IPacketDecoder` 吃 `DecoderInput`（L2 結果的 slim view，無 `Decoded` 欄位避免遞迴），回傳 POCO；pipeline 把 POCO 塞進最終 `MabiPacket.Decoded`：

```csharp
public readonly record struct DecoderInput(
    DateTime TimestampUtc,
    Direction Direction,
    ushort Op,
    ulong EntityId,
    IReadOnlyList<MessageElem> Elems);

public interface IPacketDecoder {
    ushort Op { get; }
    object Decode(DecoderInput input);   // 回傳 POCO
}

// Decoder 拋例外時 pipeline 自動降級成 L2，emit DecoderFailed
```

POCO 範例：
```csharp
public sealed record PlayerSkillPrepareStart(ushort SkillId);
public sealed record CombatActionPack(ulong AttackerId, IReadOnlyList<CombatSubAction> Sub);
public sealed record CombatSubAction(byte Type, ushort SkillId, ushort SubSkillId, ulong TargetId, int Damage);
```

**消費端 pattern match**：
```csharp
pipeline.PacketReceived += (_, p) => {
    switch (p.Decoded) {
        case CombatActionPack pack:
            foreach (var sub in pack.Sub) { /* ... */ }
            break;
        case PlayerSkillPrepareStart prep:
            Console.WriteLine($"skill prep {prep.SkillId}");
            break;
        case null:
            // 沒 L3，看 p.Elems
            break;
    }
};
```

### NDJSON 線上格式

**單一 stream，`kind` discriminator 區分**：

```json
{"kind":"event","ts":"2026-05-13T18:23:11.000Z","type":"SessionStart","region":"tw","processId":4812}
{"kind":"event","ts":"2026-05-13T18:23:11.842Z","type":"ConnectionEstablished","remote":"61.218.1.2:11000","nic":"乙太網路 4"}
{"kind":"packet","ts":"2026-05-13T18:23:12.103Z","dir":"in","op":"0x520C","opName":"EntityAppear","entityId":"12345678901234","type":"EntityAppear","decoded":{"name":"Alice","raceId":1},"elems":[{"t":"Short","v":1},{"t":"String","v":"Alice"}]}
{"kind":"event","ts":"2026-05-13T18:45:12.103Z","type":"ConnectionLost","lastRemote":"61.218.1.2:11000"}
{"kind":"event","ts":"2026-05-13T18:45:18.501Z","type":"ConnectionResumed","newRemote":"61.218.1.3:11000","sameAsLast":false}
```

**Packet 欄位約定**：

| 欄位 | 型態 | 必有 | 說明 |
|---|---|---|---|
| `kind` | `"packet"` | ✓ | discriminator |
| `ts` | ISO 8601 UTC | ✓ | 來自 pcap timestamp |
| `dir` | `"in"` / `"out"` | ✓ | 永遠檢查；不要假設只有 `"in"` |
| `op` | hex `"0xXXXX"` | ✓ | 用 hex，避免 enum decimal 對不上 |
| `opName` | string / null | ✓ | OpCodes 對得到才填 |
| `entityId` | string（uint64） | ✓ | string 避免 JS double 精度問題 |
| `type` | string / null | ✓ | L3 POCO 類別名 |
| `decoded` | object / null | ✓ | L3 POCO 序列化 |
| `elems` | array | ✓ | L2 永遠有；用 `t`/`v` 縮寫省 byte |

POCO 屬性 PascalCase → JSON camelCase（`PropertyNamingPolicy = CamelCase`）。

---

## §4 Capture / TCP 識別 / 重連

### 兩階段識別

**Step 1: Process 識別（預設）**

1. 找 process（預設 name = `"Client.exe"`）→ PID
2. `GetExtendedTcpTable` 查 PID 的 outbound `ESTABLISHED` 連線
3. 篩 1 條 → endpoint 確認；0 條 → poll 等；多條 → 套 region profile 篩、或挑非 80/443

**Step 2: Region profile fallback**

沒權限抓 process owner / process 隱藏時，用寫死的 IP/port 範圍當 BPF filter。被動等流量、第一條 server→client 的當目標。

### NIC 選擇

`Win32 GetBestInterface(remote)` → ifIndex → SharpPcap NIC list 配對。失敗時用 default gateway 的第一張 NIC。

### BPF filter

```
tcp and src host <remoteIp> and src port <remotePort>
```

`src host` 保證 inbound only（需求 #4），kernel-level 過濾，user-space CPU 節省。

### 斷線偵測 + 重連流程

```
背景 task 每 N 秒（預設 2s）查 TCP table：
   ↓
  endpoint 還在 ESTABLISHED？
   ├── 是 → 繼續
   └── 否 → emit ConnectionLost
            清掉 TCP reassembler 的 5-tuple state
            重跑 GameEndpointResolver.ResolveAsync
                ↓
            新 endpoint
                ├── 跟舊同 → emit ConnectionResumed { SameAsLast = true }
                └── 跟舊不同（換伺服/頻道）→
                    重下 BPF filter
                    新 5-tuple stream
                    emit ConnectionResumed { SameAsLast = false }
```

**重連視為「同一 session 不同段」**：consumer 不會看到 `SessionEnd` + `SessionStart`，只會看到 `ConnectionLost` + `ConnectionResumed`。

### 設定

```csharp
public sealed class CaptureOptions {
    public string[] ProcessNames { get; init; } = ["Client.exe"];
    public RegionProfile? Region { get; init; }
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);
    public bool PreferProcessDetection { get; init; } = true;
}
```

---

## §5 錄製檔案輸出

### Session 目錄結構

```
sessions/
└── 2026-05-13T18-23-11/
    ├── session.pcap            # 原始封包（Stage 0 分支）
    ├── session.events.ndjson   # session 等級事件
    └── session.json            # 整場 metadata
```

### `session.pcap`

- Stage 0 raw frame 直接寫，**不**等 pipeline 解析
- 跨重連同檔繼續寫
- SharpPcap `CaptureFileWriterDevice`

### `session.events.ndjson`

每行一筆 session 事件（SessionStart / ConnectionLost / FrameResync / BadBody / SessionEnd 等）。Replay GUI 用這個檔在 seek bar 標 marker。

### `session.json`

```json
{
  "id": "2026-05-13T18-23-11",
  "startedAt": "2026-05-13T18:23:11Z",
  "endedAt":   "2026-05-13T18:50:00Z",
  "region": "tw",
  "endpoints": [
    {"remote": "61.218.1.2:11000", "from": "...", "to": "..."},
    {"remote": "61.218.1.3:11000", "from": "...", "to": "..."}
  ],
  "stats": {
    "totalFrames": 184231,
    "totalPackets": 92117,
    "badBodyCount": 14,
    "framingResyncCount": 2
  }
}
```

### Cli stdout NDJSON

**不寫檔**，純 stdout，使用者自己 redirect。stderr 純 log（與 stdout 嚴格分離，是給跨語言消費者的契約）。

### Cli 旗標

```
mabipacade capture
  --region tw
  --process Client.exe              # 預設
  --record-pcap sessions/           # 寫三檔
  --no-stdout                       # 只錄不吐
  --filter-op 0x6984,0x7926         # NDJSON 只吐這些 op
  --decoded-only                    # 跳過沒 L3 的 packet
  --diagnostics off|on|summary      # FrameResync/BadBody/DecoderFailed 是否吐 live

mabipacade replay
  --in sessions/.../session.pcap
  --rate 2.0
  --start-at 00:10:00
  --end-at 00:15:00
```

---

## §6 ReplayTransport（forward-only）

**Controller 在 Core**，因為 CLI / Tests / GUI 都用，不只是 UI 事。

```csharp
public sealed class ReplayTransport {
    public ReplayState State { get; }              // Playing / Paused / Stopped
    public TimeSpan Position { get; }
    public TimeSpan Duration { get; }              // 開檔時掃一遍取得
    public double Rate { get; set; }               // 0.5x / 1x / 4x

    public void Play();
    public void Pause();
    public void Stop();
    public void StepForward(int n = 1);
    public void SeekForwardTo(TimeSpan t);         // 只能往前，t < Position 拋

    public event EventHandler<TimeSpan> PositionChanged;
    public event EventHandler<ReplayState> StateChanged;
}
```

**完全 forward-only**：不做 StepBackward、不做向前 SeekTo。要倒退 = reload pcap。連帶不需要建 pcap index。

**Replay 時 session events 同步回放**：讀 `session.events.ndjson` 與 pcap 做時間 merge，到達某時間點先 emit `SessionEvent` 再 emit `MabiPacket`，跟 live 體驗一致。外部給的純 pcap（沒 events sidecar）只吐 packet。

---

## §7 DebugUi（WPF）

### 單視窗、Live / Replay 共用 layout

```
┌─────────────────────────────────────────────────────────────────────────┐
│ [Source: ⬤ Live ○ Replay]  [▶ Pause] [⏭ Step] [Rate 1x ▼] [Open pcap…] │
├─────────────────────────────────────────────────────────────────────────┤
│ Filter:  Op [_______]  EntityId [_______]  ☐ Decoded only              │
├──────────────────────────┬──────────────────────────────────────────────┤
│ Packet List              │ Detail                                       │
│ (virtualized DataGrid)   │ ┌─ Decoded ─ Elems ─ Hex ─┐                  │
│                          │ │                          │                  │
├──────────────────────────┴──────────────────────────────────────────────┤
│ ● Connected  61.218.1.2:11000 │ 1,234 pps  2.1 MB/s │ Resync: 0  Bad: 0│
└─────────────────────────────────────────────────────────────────────────┘
                         （replay 模式加 seek bar）
```

### 設計重點

- **Roll buffer 50,000 筆**（Live 模式）：超過砍最舊；Replay 模式不限制
- **Filter 純 view-side**：不影響 Core pipeline / 不影響錄製
- **Auto-scroll lock**：捲到底 → follow；捲離底 → 暫停 follow
- **批次更新**：pipeline → `Channel<MabiPacket>` → UI 每 100ms tick 一次 AddRange，避免逐筆 UI thread 切換
- **Session events 三呈現**：
  - Live：status bar 顏色變化 + toast
  - Replay：seek bar marker
  - Inline：可選插入 packet list（底色區分），預設開
- **GUI-only 小功能**：Copy as JSON、Export selected to NDJSON、Pause live feed（pipeline 持續 / list 暫停）

### M3.5 — 名稱解析

**僅 DebugUi**，純 view-side enrichment：

```
NameResolution/
├── SkillNameMap.cs          # 載 SkillInfo.xml + SkillInfo.taiwan.txt
├── ConditionNameMap.cs      # 同上 for buff/debuff
└── NameResolver.cs          # SkillId → 名稱 string
```

**從筆記要遵守的雷**：
- XML 是 **UTF-16**，本地化 txt 是 **UTF-8 with BOM**
- SkillID 重複時取**最後一筆**（不是 max Season）
- `ItemDB.xml` 也含武器（不只 `itemDB_Weapon.xml`）

**不**在 DebugUi 內 spawn mabi-pack2——使用者預先解好 XML，UI 設定餵路徑進來。

### 設定持久化

**APPDIR 模式**（不用 `%AppData%`）：

```
<DebugUi.exe 所在目錄>/
├── Mabipacade.DebugUi.exe
├── settings.json     # 視窗大小、最近 pcap、預設 region
└── filters.json      # 常用 filter 組合
```

用 `AppContext.BaseDirectory` 取 exe 路徑（不依賴 cwd，避免從別處啟動找不到設定）。

---

## §8 失敗模式

| 階段 | 狀況 | 處理 |
|---|---|---|
| 啟動 | Npcap 沒裝 | `CaptureBootstrapException` + 引導訊息 |
| 啟動 | Game process 沒跑 | `ResolverState.SearchingProcess`，poll |
| 啟動 | 沒 admin 抓不到 process owner | 降級到 region profile fallback + warning |
| Capture | NIC 抓不到（VPN / 虛擬交換器漫遊） | Endpoint 變動 → 重跑 NIC selection |
| Capture | TCP table 沒看到 game 連線（process 隱藏） | timeout 後切 fallback |
| Capture | 多條符合連線 | region profile 範圍篩、剩餘挑非 80/443 |
| Stage 3 | TCP buffer 過長（corrupt / attack） | 超過上限砍 buffer + emit warning |
| Stage 4 | FramingError | realign + emit `FrameResync` |
| Stage 5 | BadBody | skip 一包 + emit `BadBody`（含 op + len） |
| Stage 6 | L3 decoder throw | 降級成 L2 + emit `DecoderFailed`（含 op + exception） |
| Stage 7 | Sink channel 滿 | drop 最舊事件 + emit `SinkOverflowEvent`，**不阻塞 pipeline** |
| Replay | pcap 損毀 | 立刻結束 + emit `SessionEnd` |

---

## §9 測試策略

### 測試金字塔

```
Manual / dogfood          DebugUi 自己用
E2E pcap replay tests     整條 pipeline + 真實 pcap fixture
Decoder tests (per op)    每個 L3 decoder 至少 happy path + edge case
Unit tests                framer / reassembler / elem reader / resolver / transport
```

### Unit（最大宗）

| 受測 | 怎麼餵 | 怎麼斷言 |
|---|---|---|
| `TcpReassembler` | 手刻 TCP segment | 重組順序、亂序、dup 丟棄 |
| `MabiPacketFramer` | 手刻 game packet bytes（含 cross-segment） | 切出 packet 數、framing error realign |
| `MessageElemReader` | 手刻 elem byte | elem 數、type、value；非法 tag → BadBody |
| `DecoderRegistry` | 註冊查詢 | op → decoder mapping |
| `GameEndpointResolver` | mock TCP table | process / region profile / fallback 路徑 |
| `ReplayTransport` | mock PcapFileFrameSource | play/pause/step/rate |

測資料一律手刻 hex，不依賴外部 fixture。

### Decoder

每個 L3 decoder 一檔 test file：
1. Happy path（筆記裡實機觀察的 shape）
2. Edge cases
3. 已知陷阱（如 `0x6993` `SkillId` 在 msg[2]、`0x698B` 不帶 SkillId）

### E2E pcap replay

從 `mabi_stage4_boss_notifier/publish/logs/` 拿幾個跑過的 pcap 進 `tests/fixtures/`，旁邊放 `*.expected.ndjson` golden file。CI 跑比對：數量不對就 fail，逼開發者明確更新 expected。

### Cli NDJSON 契約測試

跑 CLI → 解析 stdout → 斷言每行有 `kind` / `ts`、`packet` 行必有 `op` / `elems`。schema 改了測試紅、強制同步 docs。

### 不測 / 困難

- **Live capture**：依賴 Npcap + Mabinogi 連線，dogfood 驗證
- **WPF UI 自動化**：先手動驗證
- **跨網卡漫遊**：列 known manual case

---

## §10 風險與未解

| 風險 | 影響 | 緩解 |
|---|---|---|
| Mabinogi 改版改 op | Decoder 失效 / BadBody 暴增 | 一 op 一檔；E2E golden 抓 regression |
| 改版改 wire format | Stage 4-5 全壞 | Fix 集中 Core；Decoders / DebugUi 不受影響 |
| Npcap 沒裝 | 啟動失敗 | 偵測 + 引導訊息 |
| 沒 admin 抓不到 process owner | Process 識別失效 | Region profile 退路 |
| 多 NIC | 抓錯網卡 | `GetBestInterface` 依路由表挑；漏抓時手動指定 |
| 韓服加密 | v1 不支援 | 明確 out of scope |
| 筆記未解之謎（0x9093 / 0x6D62 / SharpMind 觸發） | 部分 op 解不到 L3 | 仍出 L2；標 unknown，實機驗證後再補 decoder |

### 待釐清（design doc 後續 follow-up）

- **Region profile 實際 IP/port 範圍** — 需要從社群 / mabidilmeter 設定取，design 階段先留 TBD
- **內建 decoder 的 expected.ndjson golden** — 等實作完才能生成，但 fixture pcap 來源已知

---

## §11 跟最初需求對表

| # | 需求 | 狀態 |
|---|---|---|
| 1 | 解析瑪奇封包、輸出 JSON 或其他格式 | M1（C# events）+ M2（NDJSON） |
| 2 | 偵測 Mabinogi TCP 連線、自動接上 | M1（`GameEndpointResolver`） |
| 3 | 輸出 pcap 與輸出檔 | M1（pcap）+ M2（events + summary） |
| 4 | 只錄接收端 | M1（BPF `src host` kernel-level）|
| 5 | 自動對應技能/CC 名稱 | M3.5（只在 DebugUi、view-side） |
| 6 | Debug UI 顯示當前解出封包 | M3 |
| 7 | 斷線自動重連（含跨伺服/換頻道） | M1 |
| 8 | Replay System（含 GUI 控制） | M1 引擎 + M3 GUI |

---

## 附錄：相關文件與參考

- [`mabinogi-packet-decoding` 筆記](D:/Projects/Notes/mabinogi-packet-decoding/README.md) — opcode 表、wire format、踩過的雷
- [`mabinogi-it-modding` 筆記](D:/Projects/Notes/mabinogi-it-modding/README.md) — .it 解包、SkillInfo.xml 解析（M3.5 用得到）
- `mabi_stage4_boss_notifier` — 既有 SharpPcap + PacketDotNet 實作（僅作參考、新專案全部重寫）
- `mabidilmeter-mogu-2` — 跨語言消費者範例（Go + Vite）
