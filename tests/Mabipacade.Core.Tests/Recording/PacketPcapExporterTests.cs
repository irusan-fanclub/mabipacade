using Mabipacade.Core.Model;
using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Recording;
using Mabipacade.Core.Sources;
using PacketDotNet;

namespace Mabipacade.Core.Tests.Recording;

public class PacketPcapExporterTests
{
    /// <summary>A body the elem reader accepts: prefix + one Short elem.</summary>
    private static readonly byte[] ValidBody =
        { 0x00, 0x01, 0x00, 0x02, 0xE6, 0x78 };

    private static MabiPacket Packet(byte[]? body = null) =>
        new(new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc), Direction.Inbound,
            0x00006984, 0x0010_0000_0000_0001UL, Array.Empty<MessageElem>(), Decoded: null)
        { Body = body ?? ValidBody };

    [Fact]
    public void CanExport_IsFalse_WithoutABody()
    {
        var bare = new MabiPacket(DateTime.UtcNow, Direction.Inbound, 1, 0UL,
            Array.Empty<MessageElem>(), null);
        Assert.False(PacketPcapExporter.CanExport(bare));
        Assert.True(PacketPcapExporter.CanExport(Packet()));
    }

    [Fact]
    public void BuildFrame_IsParseableTcp_CarryingTheReframedPacket()
    {
        var frame = PacketPcapExporter.BuildFrame(Packet());

        var tcp = PacketDotNet.Packet.ParsePacket(LinkLayers.Ethernet, frame).Extract<TcpPacket>();
        Assert.NotNull(tcp);
        var payload = tcp!.PayloadData;

        // Wire framing round-trips through the production framer.
        var result = MabiPacketFramer.TryReadOne(payload, out var slice, out int consumed);
        Assert.Equal(FrameResult.Ok, result);
        Assert.Equal(payload.Length, consumed);
        Assert.Equal(0x00006984u, slice!.Op);
        Assert.Equal(0x0010_0000_0000_0001UL, slice.EntityId);
        Assert.Equal(ValidBody, slice.Body);
    }

    [Fact]
    public async Task ExportedFile_ReplaysThroughThePipeline_AsTheSamePacket()
    {
        var original = Packet();
        var path = Path.Combine(Path.GetTempPath(), $"mabipacade-test-{Guid.NewGuid():N}.pcapng");
        try
        {
            PacketPcapExporter.Export(original, path);

            using var source = new PcapFileFrameSource(path);
            var pipeline = new PacketPipeline(source, new DecoderRegistry());
            var packets = new List<MabiPacket>();
            pipeline.PacketReceived += (_, p) => packets.Add(p);

            await pipeline.StartAsync(CancellationToken.None);
            await pipeline.StopAsync();

            var replayed = Assert.Single(packets);
            Assert.Equal(original.Op, replayed.Op);
            Assert.Equal(original.EntityId, replayed.EntityId);
            Assert.Equal(original.Body, replayed.Body);
            // pcapng stores nanoseconds, but the reading stack may round to
            // microseconds; the wall-clock second is what matters for replay.
            Assert.True((replayed.TimestampUtc - original.TimestampUtc).Duration()
                        < TimeSpan.FromMilliseconds(1));
        }
        finally
        {
            try { File.Delete(path); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task MultiPacketExport_ReplaysAllPackets_InOrder()
    {
        // Same flow, so replay only works if the synthesized sequence numbers
        // advance frame to frame; identical ones would read as retransmits.
        var t0 = new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc);
        var originals = new[]
        {
            Packet() with { TimestampUtc = t0 },
            Packet(new byte[] { 0x00, 0x01, 0x00, 0x01, 0x2A }) with { TimestampUtc = t0.AddMilliseconds(5), Op = 0x00001111 },
            Packet(new byte[] { 0x00, 0x00, 0x00 }) with { TimestampUtc = t0.AddMilliseconds(9), Op = 0x00002222 },
        };
        var path = Path.Combine(Path.GetTempPath(), $"mabipacade-test-{Guid.NewGuid():N}.pcapng");
        try
        {
            PacketPcapExporter.Export(originals, path);

            using var source = new PcapFileFrameSource(path);
            var pipeline = new PacketPipeline(source, new DecoderRegistry());
            var packets = new List<MabiPacket>();
            pipeline.PacketReceived += (_, p) => packets.Add(p);

            await pipeline.StartAsync(CancellationToken.None);
            await pipeline.StopAsync();

            Assert.Equal(3, packets.Count);
            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(originals[i].Op, packets[i].Op);
                Assert.Equal(originals[i].Body, packets[i].Body);
            }
        }
        finally
        {
            try { File.Delete(path); } catch (IOException) { }
        }
    }

    [Fact]
    public void Export_WithoutABody_Throws()
    {
        var bare = new MabiPacket(DateTime.UtcNow, Direction.Inbound, 1, 0UL,
            Array.Empty<MessageElem>(), null);
        Assert.Throws<InvalidOperationException>(() =>
            PacketPcapExporter.BuildFrame(bare));
    }
}
