using System.Buffers.Binary;
using Mabipacade.Core.Recording;
using Mabipacade.Core.Sources;
using PacketDotNet;

namespace Mabipacade.Core.Tests.Recording;

public class PcapNgWriterTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"mabipacade-test-{Guid.NewGuid():N}.pcapng");

    private static async Task<List<RawFrameEventArgs>> ReadBackAsync(string path)
    {
        var frames = new List<RawFrameEventArgs>();
        using var src = new PcapFileFrameSource(path);
        src.FrameReceived += (_, e) => { lock (frames) frames.Add(e); };
        await src.StartAsync(CancellationToken.None);
        await src.StopAsync();
        return frames;
    }

    [Fact]
    public async Task WrittenFrames_ReadBackThroughTheRealReader()
    {
        var path = TempPath();
        var a = new byte[] { 1, 2, 3, 4 };
        var b = new byte[] { 5, 6, 7, 8, 9 };   // odd length, exercises block padding
        try
        {
            using (var w = new PcapNgWriter(path, LinkLayers.Ethernet))
            {
                w.Write(a, DateTime.UnixEpoch.AddSeconds(1));
                w.Write(b, DateTime.UnixEpoch.AddSeconds(2));
            }

            var frames = await ReadBackAsync(path);
            Assert.Equal(2, frames.Count);
            Assert.Equal(a, frames[0].Data);
            Assert.Equal(b, frames[1].Data);
            Assert.All(frames, f => Assert.Equal(LinkLayers.Ethernet, f.LinkLayer));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Timestamps_AreRecordedToTheNanosecond()
    {
        // Classic pcap has a fixed microsecond field, so the old writer dropped
        // everything below it. The interface block here declares nanosecond
        // resolution, so a DateTime's 100ns tick lands in the file exactly —
        // which is what Wireshark and any ns-aware reader will show.
        var path = TempPath();
        var ts = DateTime.UnixEpoch.AddSeconds(1234).AddTicks(5678901);
        try
        {
            using (var w = new PcapNgWriter(path, LinkLayers.Ethernet))
                w.Write(new byte[] { 0xAA }, ts);

            var bytes = File.ReadAllBytes(path);
            int epb = SkipBlocks(bytes, 2);   // past the section header and interface block
            ulong high = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(epb + 12));
            ulong low = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(epb + 16));
            ulong nanos = (high << 32) | low;

            Assert.Equal((ulong)(ts - DateTime.UnixEpoch).Ticks * 100UL, nanos);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task ReadingBack_LosesSubMicroseconds_BecauseOfSharpPcap()
    {
        // Pinning a limitation of our own reader, not of the file: SharpPcap
        // surfaces timestamps through PosixTimeval, whose only fields are
        // seconds and microseconds. The nanoseconds are in the file — anything
        // that reads them will see them — but this path cannot express them.
        var path = TempPath();
        var ts = DateTime.UnixEpoch.AddSeconds(1234).AddTicks(5678901);
        try
        {
            using (var w = new PcapNgWriter(path, LinkLayers.Ethernet))
                w.Write(new byte[] { 0xAA }, ts);

            var read = Assert.Single(await ReadBackAsync(path)).TimestampUtc;
            Assert.Equal(ts.AddTicks(-1), read);   // the trailing 100ns is gone
            Assert.True(ts - read < TimeSpan.FromTicks(10));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    /// <summary>Offset of the block after <paramref name="count"/> leading blocks.</summary>
    private static int SkipBlocks(byte[] bytes, int count)
    {
        int offset = 0;
        for (int i = 0; i < count; i++)
            offset += (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4));
        return offset;
    }

    [Fact]
    public void EmptyCapture_StillWritesAValidHeader()
    {
        // A session that ends before a single frame arrives must leave a file
        // Wireshark can open, not a zero-byte stub.
        var path = TempPath();
        try
        {
            using (var _ = new PcapNgWriter(path, LinkLayers.Ethernet)) { }

            var bytes = File.ReadAllBytes(path);
            Assert.Equal(0x0A0D0D0AU, BinaryPrimitives.ReadUInt32LittleEndian(bytes));      // SHB
            Assert.Equal(0x1A2B3C4DU, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8)));
            // Section header then interface description, nothing else.
            uint shbLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4));
            Assert.Equal(1U, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan((int)shbLength)));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void EveryBlock_IsFourByteAlignedAndSelfDelimiting()
    {
        // pcapng repeats each block's total length at its end; a reader that
        // walks the chain lands exactly on the file's end when the writer got
        // the padding right.
        var path = TempPath();
        try
        {
            using (var w = new PcapNgWriter(path, LinkLayers.Ethernet))
            {
                w.Write(new byte[] { 1 }, DateTime.UnixEpoch);          // 1 byte -> 3 pad
                w.Write(new byte[] { 1, 2, 3 }, DateTime.UnixEpoch);    // 3 bytes -> 1 pad
                w.Write(new byte[] { 1, 2, 3, 4 }, DateTime.UnixEpoch); // 4 bytes -> 0 pad
            }

            var bytes = File.ReadAllBytes(path);
            int offset = 0, blocks = 0;
            while (offset < bytes.Length)
            {
                uint length = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4));
                Assert.Equal(0U, length % 4);
                uint trailing = BinaryPrimitives.ReadUInt32LittleEndian(
                    bytes.AsSpan(offset + (int)length - 4));
                Assert.Equal(length, trailing);
                offset += (int)length;
                blocks++;
            }
            Assert.Equal(bytes.Length, offset);
            Assert.Equal(5, blocks);   // SHB + IDB + three packet blocks
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
