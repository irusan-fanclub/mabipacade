using System.IO;
using Mabipacade.Core.Capture;
using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Sources;
using Mabipacade.Decoders;

namespace Mabipacade.DebugUi.Services;

public sealed record LiveSession(PipelineHost Host, IFrameSource Source, GameEndpoint Endpoint);

public static class LiveSessionFactory
{
    public sealed class BootstrapException : Exception
    {
        public BootstrapException(string message) : base(message) { }
    }

    public static LiveSession Create(string regionName, string processName, IUiDispatcher dispatcher)
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

        var bpf = $"tcp and src host {endpoint.RemoteAddress} and src port {endpoint.RemotePort}";
        var source = new LiveFrameSource(device, bpf);
        var registry = new DecoderRegistry();
        DefaultDecoders.RegisterAll(registry);
        var pipeline = new PacketPipeline(source, registry);
        var host = new PipelineHost(pipeline, dispatcher);

        return new LiveSession(host, source, endpoint);
    }
}
