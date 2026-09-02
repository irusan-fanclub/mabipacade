using Mabipacade.Core.Sources;
using Mabipacade.Core.Tests.Pipeline;

namespace Mabipacade.Core.Tests.Sources;

public class PcapFileFrameSourceTests
{
    private static byte[] Frame(ushort srcPort)
        => TestEthernetBuilder.WrapTcp(new byte[] { 1, 2, 3, 4 }, srcPort, dstPort: 50000);

    private static async Task<(int frames, bool eos, PcapFileFrameSource src)> ReadAsync(string path)
    {
        int frames = 0;
        bool eos = false;
        var src = new PcapFileFrameSource(path);
        src.FrameReceived += (_, _) => Interlocked.Increment(ref frames);
        src.EndOfStream += (_, _) => eos = true;

        await src.StartAsync(CancellationToken.None);
        await src.StopAsync();
        return (frames, eos, src);
    }

    [Fact]
    public async Task EmitsFrames_FromPcapNg()
    {
        using var file = CaptureFileBuilder.WriteTemp(
            CaptureFileBuilder.PcapNg(Frame(11000), Frame(11022), Frame(11022)), ".pcapng");

        var (frames, eos, src) = await ReadAsync(file.Path);
        using (src)
        {
            Assert.Equal(3, frames);
            Assert.True(eos);
            Assert.False(src.Truncated);
        }
    }

    [Fact]
    public async Task EmitsFrames_FromClassicPcap()
    {
        using var file = CaptureFileBuilder.WriteTemp(
            CaptureFileBuilder.Pcap(Frame(11000), Frame(11022)), ".pcap");

        var (frames, eos, src) = await ReadAsync(file.Path);
        using (src)
        {
            Assert.Equal(2, frames);
            Assert.True(eos);
            Assert.False(src.Truncated);
        }
    }

    [Fact]
    public async Task TruncatedTail_KeepsFramesRead_AndEndsCleanly()
    {
        // Recordings from other tools are routinely cut mid-record when the
        // process is killed. The frames before the cut are intact, so the read
        // must end like any other end-of-file rather than fail.
        using var file = CaptureFileBuilder.WriteTemp(
            CaptureFileBuilder.PcapNgWithTruncatedTail(Frame(11000), Frame(11022), Frame(11022)),
            ".pcapng");

        var (frames, eos, src) = await ReadAsync(file.Path);
        using (src)
        {
            Assert.Equal(2, frames);
            Assert.True(eos);
            Assert.True(src.Truncated);
        }
    }

    [Fact]
    public async Task NoReadableFrame_StillFails()
    {
        // Nothing was read, so "truncated tail" is not a defensible reading —
        // the file is broken and silence would hide that.
        using var file = CaptureFileBuilder.WriteTemp(
            CaptureFileBuilder.PcapNgWithNoReadableFrame(Frame(11000)), ".pcapng");

        using var src = new PcapFileFrameSource(file.Path);
        await src.StartAsync(CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => src.StopAsync());
    }
}
