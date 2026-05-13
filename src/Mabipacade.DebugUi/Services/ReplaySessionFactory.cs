using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Replay;
using Mabipacade.Core.Sources;
using Mabipacade.Decoders;

namespace Mabipacade.DebugUi.Services;

public sealed record ReplaySession(PipelineHost Host, ReplayTransport Transport);

public static class ReplaySessionFactory
{
    public static ReplaySession Create(string pcapPath, IUiDispatcher dispatcher)
    {
        var rawSource = new PcapFileFrameSource(pcapPath);
        var transport = new ReplayTransport(rawSource);
        var registry = new DecoderRegistry();
        DefaultDecoders.RegisterAll(registry);
        // Pipeline subscribes to the TRANSPORT, not the raw source.
        // This way pause-gate + rate-throttle in the transport actually gate decoding.
        var pipeline = new PacketPipeline(transport, registry);
        var host = new PipelineHost(pipeline, dispatcher);
        return new ReplaySession(host, transport);
    }
}
