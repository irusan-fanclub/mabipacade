# Installing Mabipacade

## Prerequisites

### 1. .NET 10 Runtime

Required for all three executables (Cli, Server, DebugUi).

Download: <https://dotnet.microsoft.com/download/dotnet/10.0>

- For **Cli** and **Server** — install **".NET 10 Runtime"** (smallest).
- For **DebugUi** — install **".NET Desktop Runtime 10"** (includes WPF).
- If you'll build from source, install **.NET 10 SDK** instead (includes everything above).

Verify:
```powershell
dotnet --list-runtimes
```
You should see `Microsoft.NETCore.App 10.x.x` (for Cli/Server) and `Microsoft.WindowsDesktop.App 10.x.x` (for DebugUi).

### 2. Npcap

Required for live capture. Pcap replay also requires Npcap because SharpPcap loads its native library at startup.

Download: <https://npcap.com/>

**During install:** tick *"Install Npcap in WinPcap API-compatible Mode"*. Without it SharpPcap won't find the capture devices.

Verify:
```powershell
Get-Service npcap | Format-Table Status, Name, DisplayName
```
Status should be `Running`.

## Install the binaries

Download the latest release from <https://github.com/irusan-fanclub/mabipacade/releases>.

The release zip contains:
```
mabipacade-<version>-win-x64/
├── cli/
│   └── mabipacade.exe
├── server/
│   └── mabipacade-server.exe
└── debugui/
    └── Mabipacade.DebugUi.exe
```

Unzip anywhere — the exes are self-contained (apart from the runtimes above). Add `cli/` and `server/` to your `PATH` if you want `mabipacade` and `mabipacade-server` available from any shell.

## First run

### DebugUi

Double-click `Mabipacade.DebugUi.exe`. The window opens immediately.

Settings (window size, last pcap, XML data dir for name resolution) are saved to `settings.json` in the same folder as the exe. If you want skill name resolution, edit it:
```json
{
  "xmlDataDirectory": "C:\\path\\to\\extracted\\mabi\\data"
}
```
See README §"Name resolution" for layout details.

### Cli

```powershell
mabipacade replay --in path/to/session.pcap
mabipacade capture --region tw
```

Cli writes NDJSON to stdout, logs to stderr. Pipe stdout to your consumer:
```powershell
mabipacade capture --region tw | python damage_meter.py
```

### Server

```powershell
mabipacade-server capture --region tw --port 9876
```
Then connect a WebSocket client to `ws://127.0.0.1:9876`. See README §"WebSocket server" for the subscribe protocol.

## Build from source

```powershell
git clone https://github.com/irusan-fanclub/mabipacade.git
cd mabipacade
dotnet test
.\scripts\publish.ps1
```
Output lands in `artifacts/`.

## Troubleshooting

**"No interfaces found" / "Couldn't find PacketCommunicatorMode_Capture"** — Npcap not installed or WinPcap-compatible mode disabled. Reinstall Npcap and tick the compatibility checkbox.

**"FileNotFoundException: System.Net.NetworkInformation" or similar at startup** — wrong .NET runtime. Install the matching runtime (or the SDK).

**"No game endpoint found"** — Mabinogi (`Client.exe`) isn't running, or you're not on a server in the region profile's IP range. Run the GUI in Replay mode against a saved pcap as a sanity check.

**Korean server packets unreadable** — Mabinogi KR encrypts traffic differently; this toolchain does not support KR. See README §"Limitations".
