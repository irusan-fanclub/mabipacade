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
        // The file does not say which region it came from; the TW port range is
        // what tells client→server frames apart in a both-direction capture,
        // and it labels inbound-only captures exactly as before.
        // Decrypt outbound too: a both-direction capture carries the seed at each
        // connection's start. An inbound-only file has no outbound to decrypt, so
        // this only ever helps.
        var pipeline = new PacketPipeline(transport, registry,
            Mabipacade.Core.Pipeline.DirectionClassifiers.ByServerEndpoint(
                Mabipacade.Core.Capture.RegionProfiles.Taiwan),
            decryptOutbound: true);
        var host = new PipelineHost(pipeline, dispatcher);
        return new ReplaySession(host, transport);
    }
}
