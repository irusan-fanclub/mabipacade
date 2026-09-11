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
        var mabiBytes = TestPacketBuilder.BuildNormal(op: 0x00006984, entityId: 0UL, bodyTail: new byte[] { 0x00, 0x00, 0x00 });
        var frame = TestEthernetBuilder.WrapTcp(mabiBytes, srcPort: 11000, dstPort: 50000);

        var source = new PipelineFakeFrameSource(frame);
        var registry = new DecoderRegistry();
        var pipeline = new PacketPipeline(source, registry);

        var packets = new List<Mabipacade.Core.Model.MabiPacket>();
        pipeline.PacketReceived += (_, p) => packets.Add(p);

        await pipeline.StartAsync(CancellationToken.None);
        await pipeline.StopAsync();

        Assert.Single(packets);
        Assert.Equal((uint)0x00006984, packets[0].Op);
    }

    [Fact]
    public async Task ClassifiesDirection_ByWhichEndIsTheServer()
    {
        // The same client→server frame: labelled outbound with the classifier,
        // inbound without one — the default that keeps old callers unchanged.
        var mabiBytes = TestPacketBuilder.BuildNormal(op: 0x00001234, entityId: 1UL, bodyTail: new byte[] { 0x00, 0x00, 0x00 });
        var frame = TestEthernetBuilder.WrapTcp(mabiBytes, srcPort: 50000, dstPort: 11022);

        async Task<Mabipacade.Core.Model.MabiPacket> RunAsync(DirectionClassifier? classifier)
        {
            var source = new PipelineFakeFrameSource(frame);
            var pipeline = new PacketPipeline(source, new DecoderRegistry(), classifier);
            var packets = new List<Mabipacade.Core.Model.MabiPacket>();
            pipeline.PacketReceived += (_, p) => packets.Add(p);
            await pipeline.StartAsync(CancellationToken.None);
            await pipeline.StopAsync();
            return Assert.Single(packets);
        }

        var classified = await RunAsync(DirectionClassifiers.ByServerEndpoint(
            Mabipacade.Core.Capture.RegionProfiles.Taiwan));
        Assert.Equal(Mabipacade.Core.Model.Direction.Outbound, classified.Direction);

        var defaulted = await RunAsync(null);
        Assert.Equal(Mabipacade.Core.Model.Direction.Inbound, defaulted.Direction);
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
        var mabiBytes = TestPacketBuilder.BuildNormal(op: 0x00006984, entityId: 0UL, bodyTail: new byte[] { 0x00, 0x00, 0x00 });
        var frame = TestEthernetBuilder.WrapTcp(mabiBytes, srcPort: 11000, dstPort: 50000);

        var source = new PipelineFakeFrameSource(frame);
        var registry = new DecoderRegistry();
        var pipeline = new PacketPipeline(source, registry);

        await pipeline.StartAsync(CancellationToken.None);
        await pipeline.StopAsync();

        Assert.Equal(1, pipeline.Metrics.TotalFrames);
        Assert.Equal(1, pipeline.Metrics.TotalPackets);
    }

    [Fact]
    public async Task SkipsTheConnectionKey_SoTheFirstPacketAfterItDecodes()
    {
        // Every connection opens with a 4-byte encryption key carrying no game
        // data. Framing it yields a garbage length, which resets the stream and
        // takes the first real packet with it — including the full-character
        // snapshot the server sends immediately after a channel switch. A game
        // packet cannot be 4 bytes: the header alone is 6.
        var key = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var mabiBytes = TestPacketBuilder.BuildNormal(op: 0x00005209, entityId: 0UL,
            bodyTail: new byte[] { 0x00, 0x00, 0x00 });

        var source = new SequenceFakeFrameSource(
            TestEthernetBuilder.WrapTcp(key, srcPort: 11022, dstPort: 50000, sequenceNumber: 1000),
            TestEthernetBuilder.WrapTcp(mabiBytes, srcPort: 11022, dstPort: 50000, sequenceNumber: 1004));

        var pipeline = new PacketPipeline(source, new DecoderRegistry());
        var packets = new List<Mabipacade.Core.Model.MabiPacket>();
        pipeline.PacketReceived += (_, p) => packets.Add(p);

        await pipeline.StartAsync(CancellationToken.None);
        await pipeline.StopAsync();

        Assert.Equal((uint)0x00005209, Assert.Single(packets).Op);
        Assert.Equal(0, pipeline.Metrics.FrameResyncCount);
    }

    [Fact]
    public async Task DropsTruncatedFrame_WithoutThrowing()
    {
        // Foreign captures taken with a small snaplen cut frames mid-header.
        // Parsing one must not escape into the capture callback, which would
        // kill the capture thread and take the rest of the file with it.
        var full = TestEthernetBuilder.WrapTcp(new byte[] { 1, 2, 3, 4 }, srcPort: 11000, dstPort: 50000);
        var truncated = full.AsSpan(0, 20).ToArray();

        var source = new PipelineFakeFrameSource(truncated);
        var registry = new DecoderRegistry();
        var pipeline = new PacketPipeline(source, registry);

        await pipeline.StartAsync(CancellationToken.None);
        await pipeline.StopAsync();

        Assert.Equal(1, pipeline.Metrics.TotalFrames);
        Assert.Equal(0, pipeline.Metrics.TotalPackets);
        Assert.Equal(1, pipeline.Metrics.MalformedFrameCount);
    }
}

/// <summary>Emits several frames in order, then end-of-stream.</summary>
internal sealed class SequenceFakeFrameSource : Mabipacade.Core.Sources.IFrameSource
{
    private readonly byte[][] _frames;
    public SequenceFakeFrameSource(params byte[][] frames) { _frames = frames; }
    public event EventHandler<RawFrameEventArgs>? FrameReceived;
    public event EventHandler? EndOfStream;
    public Task StartAsync(CancellationToken ct)
    {
        foreach (var f in _frames)
            FrameReceived?.Invoke(this, new RawFrameEventArgs(f, LinkLayers.Ethernet, DateTime.UnixEpoch));
        EndOfStream?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
    public Task StopAsync() => Task.CompletedTask;
    public void Dispose() { }
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
