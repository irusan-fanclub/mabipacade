using PacketDotNet;
using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Sources;

namespace Mabipacade.Core.Tests.Pipeline;

public class PacketPipelineTests
{
    [Fact]
    public async Task EmitsPacket_ForSingleHandCraftedFrame()
    {
        // bodyTail must be a valid MessageElemReader body: [outer uvarint=0][count=0][reserved 0]
        var mabiBytes = TestPacketBuilder.BuildNormal(op: 0x6984, entityId: 0UL, bodyTail: new byte[] { 0x00, 0x00, 0x00 });
        var frame = TestEthernetBuilder.WrapTcp(mabiBytes, srcPort: 11000, dstPort: 50000);

        var source = new PipelineFakeFrameSource(frame);
        var registry = new DecoderRegistry();
        var pipeline = new PacketPipeline(source, registry);

        var packets = new List<Mabipacade.Core.Model.MabiPacket>();
        pipeline.PacketReceived += (_, p) => packets.Add(p);

        await pipeline.StartAsync(CancellationToken.None);
        await pipeline.StopAsync();

        Assert.Single(packets);
        Assert.Equal((ushort)0x6984, packets[0].Op);
    }

    [Fact]
    public async Task EmitsSessionEnd_OnEndOfStream()
    {
        var source = new PipelineFakeFrameSource(null);
        var registry = new DecoderRegistry();
        var pipeline = new PacketPipeline(source, registry);

        var events = new List<Mabipacade.Core.Diagnostics.SessionEvent>();
        pipeline.SessionEventReceived += (_, e) => events.Add(e);

        await pipeline.StartAsync(CancellationToken.None);
        await pipeline.StopAsync();

        Assert.Single(events);
        Assert.IsType<Mabipacade.Core.Diagnostics.SessionEvent.SessionEnd>(events[0]);
    }

    [Fact]
    public async Task MetricsIncrement_OnValidPacket()
    {
        var mabiBytes = TestPacketBuilder.BuildNormal(op: 0x6984, entityId: 0UL, bodyTail: new byte[] { 0x00, 0x00, 0x00 });
        var frame = TestEthernetBuilder.WrapTcp(mabiBytes, srcPort: 11000, dstPort: 50000);

        var source = new PipelineFakeFrameSource(frame);
        var registry = new DecoderRegistry();
        var pipeline = new PacketPipeline(source, registry);

        await pipeline.StartAsync(CancellationToken.None);
        await pipeline.StopAsync();

        Assert.Equal(1, pipeline.Metrics.TotalFrames);
        Assert.Equal(1, pipeline.Metrics.TotalPackets);
    }
}

internal sealed class PipelineFakeFrameSource : Mabipacade.Core.Sources.IFrameSource
{
    private readonly byte[]? _frame;
    public PipelineFakeFrameSource(byte[]? frame) { _frame = frame; }
    public event EventHandler<RawFrameEventArgs>? FrameReceived;
    public event EventHandler? EndOfStream;
    public Task StartAsync(CancellationToken ct)
    {
        if (_frame is not null)
            FrameReceived?.Invoke(this, new RawFrameEventArgs(_frame, PacketDotNet.LinkLayers.Ethernet, DateTime.UtcNow));
        EndOfStream?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
    public Task StopAsync() => Task.CompletedTask;
    public void Dispose() { }
}
