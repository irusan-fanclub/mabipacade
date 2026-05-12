using Mabipacade.Core.Diagnostics;
using Mabipacade.Core.Model;
using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Sources;
using Mabipacade.Decoders;

namespace Mabipacade.Core.Tests.EndToEnd;

public class PipelineE2ETests
{
    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "fixtures", "known_good.pcap");

    [Fact(Skip = "Requires local fixture — copy a pcap to tests/Mabipacade.Core.Tests/fixtures/known_good.pcap")]
    public async Task FullPipeline_ProducesPackets_FromRealPcap()
    {
        if (!File.Exists(FixturePath)) return;

        using var source = new PcapFileFrameSource(FixturePath);
        var registry = new DecoderRegistry();
        DefaultDecoders.RegisterAll(registry);
        var pipeline = new PacketPipeline(source, registry);

        var packets = new List<MabiPacket>();
        var events  = new List<SessionEvent>();
        pipeline.PacketReceived += (_, p) => packets.Add(p);
        pipeline.SessionEventReceived += (_, e) => events.Add(e);

        await pipeline.StartAsync(CancellationToken.None);
        await Task.Delay(2000);
        await pipeline.StopAsync();

        Assert.True(packets.Count > 0, "expected at least one decoded packet");
        Assert.Empty(events.OfType<SessionEvent.FrameResync>());
        int badBodyRate = events.OfType<SessionEvent.BadBody>().Count();
        Assert.True(badBodyRate < packets.Count, "more BadBody than packets — framer likely broken");
    }
}
