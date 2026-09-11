using System.IO;
using Mabipacade.Core.Capture;
using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Sources;
using Mabipacade.Decoders;

namespace Mabipacade.DebugUi.Services;

/// <summary>
/// A running live capture. <see cref="NicDescription"/> and
/// <see cref="CaptureFilter"/> are carried so a recording can record what it
/// was taken from, which pcapng stores in its interface block.
/// </summary>
public sealed record LiveSession(
    PipelineHost Host,
    IFrameSource Source,
    GameEndpoint Endpoint,
    string NicDescription,
    string CaptureFilter,
    // Polled by the view model to keep the filter current and to report
    // endpoint changes; the session owns it but does not run it.
    CaptureSession Watchdog);

public static class LiveSessionFactory
{
    public sealed class BootstrapException : Exception
    {
        public BootstrapException(string message) : base(message) { }
    }

    /// <summary>
    /// <paramref name="captureOutbound"/> widens the BPF filter to both
    /// directions and classifies frames by which end is the game server; off,
    /// the capture admits only server→client frames, as before. Outbound
    /// framing is assumed to match inbound — flows are reassembled separately,
    /// so if the client frames differently the outbound flow resyncs without
    /// disturbing the inbound one.
    /// </summary>
    public static LiveSession Create(string regionName, string processName, IUiDispatcher dispatcher,
        bool captureOutbound = false)
    {
        var region = regionName switch
        {
            "tw" => RegionProfiles.Taiwan,
            "jp" => RegionProfiles.Japan,
            "kr" => RegionProfiles.Korea,
            _ => throw new BootstrapException($"Unknown region '{regionName}'")
        };

        var procFinder = new ProcessFinder();
        var pid = procFinder.Find(Path.GetFileNameWithoutExtension(processName));

        var tcpTable = new Win32TcpConnectionTable();
        var resolver = new GameEndpointResolver(tcpTable, pid, region);
        var endpoint = resolver.TryResolveOnce()
            ?? throw new BootstrapException("No game endpoint found. Is the game running?");

        var nicSelector = new Win32NicSelector();
        var device = nicSelector.SelectFor(endpoint.RemoteAddress)
            ?? throw new BootstrapException($"No NIC routes to {endpoint.RemoteAddress}");

        // Cover the server's whole network so a channel switch is captured as
        // soon as it opens; the port vet below decides what is actually ours.
        var tracker = new ClientConnectionTracker(tcpTable, pid);
        var bpf = BpfFilter.ForConnections(tracker.Snapshot(), captureOutbound)
            ?? BpfFilter.ForAddress(endpoint.RemoteAddress, captureOutbound)
            ?? $"tcp and {(captureOutbound ? "host" : "src host")} {endpoint.RemoteAddress}";

        // With an inbound-only filter this labels everything inbound anyway,
        // so the classifier can be unconditional.
        var classifier = DirectionClassifiers.ByServerEndpoint(region);

        var live = new LiveFrameSource(device, bpf);
        // Upstream of both the recorder and the pipeline, so a virtual machine
        // sharing this host's address never reaches either.
        var source = new ClientTrafficFilterSource(live, tracker.IsClientLocalPort, classifier);

        var registry = new DecoderRegistry();
        DefaultDecoders.RegisterAll(registry);
        // Only decrypt when we are actually capturing the client's traffic; the
        // seed is read from each connection's opening bytes.
        var pipeline = new PacketPipeline(source, registry, classifier, decryptOutbound: captureOutbound);
        var host = new PipelineHost(pipeline, dispatcher);

        var nicDescription = device is SharpPcap.LibPcap.LibPcapLiveDevice lp
            ? lp.Description ?? lp.Name
            : device.ToString() ?? "unknown";

        var watchdog = new CaptureSession(tcpTable, pid, region, applyFilter: live.SetFilter,
            bothDirections: captureOutbound);

        return new LiveSession(host, source, endpoint, nicDescription, bpf, watchdog);
    }
}
