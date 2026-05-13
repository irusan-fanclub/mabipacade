using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Replay;
using Mabipacade.Core.Sources;
using Mabipacade.Decoders;

namespace Mabipacade.DebugUi.Services;

public sealed record ReplaySession(PipelineHost Host, ReplayTransport Transport, IFrameSource Source);

public static class ReplaySessionFactory
{
    public static ReplaySession Create(string pcapPath, IUiDispatcher dispatcher)
    {
        var source = new PcapFileFrameSource(pcapPath);
        var transport = new ReplayTransport(source);
        var registry = new DecoderRegistry();
        DefaultDecoders.RegisterAll(registry);
        var pipeline = new PacketPipeline(source, registry);
        var host = new PipelineHost(pipeline, dispatcher);
        return new ReplaySession(host, transport, source);
    }
}
