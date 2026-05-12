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
using SharpPcap.LibPcap;

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

        var deviceDesc = device is LibPcapLiveDevice lp ? lp.Description : device.ToString() ?? "unknown";
        var bpf = $"tcp and src host {endpoint.RemoteAddress} and src port {endpoint.RemotePort}";
        StderrLogger.Info($"Capturing on {deviceDesc} with filter: {bpf}");

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
