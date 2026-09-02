using System.CommandLine;
using Mabipacade.Core.Capture;
using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Sources;
using Mabipacade.Decoders;
using Mabipacade.Server.Logging;
using Mabipacade.Server.WebSocket;
using SharpPcap.LibPcap;

namespace Mabipacade.Server.Commands;

internal static class ServeCaptureCommand
{
    public static Command Build()
    {
        var port = new Option<int>("--port") { Description = "WS port (default 9876)", DefaultValueFactory = _ => 9876 };
        var region = new Option<string>("--region") { Description = "Region: tw|jp|kr (default tw)", DefaultValueFactory = _ => "tw" };
        var process = new Option<string>("--process") { Description = "Process name (default Client.exe)", DefaultValueFactory = _ => "Client.exe" };

        var cmd = new Command("capture", "Serve live Mabinogi traffic over WebSocket")
        {
            port, region, process
        };
        cmd.SetAction(parse => Run(parse.GetValue(port), parse.GetValue(region)!, parse.GetValue(process)!));
        return cmd;
    }

    private static int Run(int port, string regionName, string processName)
    {
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

        var tcpTable = new Win32TcpConnectionTable();
        var resolver = new GameEndpointResolver(tcpTable, pid, region);
        var endpoint = resolver.TryResolveOnce();
        if (endpoint is null)
        {
            StderrLogger.Error("No game endpoint found.");
            return 3;
        }

        var nicSelector = new Win32NicSelector();
        var device = nicSelector.SelectFor(endpoint.RemoteAddress);
        if (device is null) { StderrLogger.Error($"No NIC routes to {endpoint.RemoteAddress}"); return 3; }

        var deviceDesc = device is LibPcapLiveDevice lp ? lp.Description : device.ToString() ?? "unknown";

        // Cover the server's whole network so a channel switch is captured at
        // once; the port vet decides what is actually ours.
        var tracker = new ClientConnectionTracker(tcpTable, pid);
        var bpf = BpfFilter.ForConnections(tracker.Snapshot())
            ?? BpfFilter.ForAddress(endpoint.RemoteAddress)
            ?? $"tcp and src host {endpoint.RemoteAddress}";

        using var host = new WebSocketHost(port, OpCodeNames.TryGetName);
        host.Start();

        var live = new LiveFrameSource(device, bpf);
        using var source = new ClientTrafficFilterSource(live, tracker.IsClientLocalPort);
        var registry = new DecoderRegistry();
        DefaultDecoders.RegisterAll(registry);
        var pipeline = new PacketPipeline(source, registry);

        pipeline.PacketReceived += (_, p) => host.BroadcastPacket(p);
        pipeline.SessionEventReceived += (_, ev) => host.BroadcastEvent(ev);

        StderrLogger.Info($"capturing on {deviceDesc} ({bpf}) → ws://127.0.0.1:{port}");
        pipeline.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

        StderrLogger.Info("Press Ctrl+C to stop.");
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        var watchdog = new CaptureSession(tcpTable, pid, region, applyFilter: filter =>
        {
            live.SetFilter(filter);
            StderrLogger.Info($"capture filter -> {filter}");
        });
        watchdog.SessionEventReceived += (_, ev) => host.BroadcastEvent(ev);
        _ = watchdog.RunAsync(cts.Token);
        try { Task.Delay(Timeout.Infinite, cts.Token).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { }

        pipeline.StopAsync().GetAwaiter().GetResult();
        return 0;
    }
}
