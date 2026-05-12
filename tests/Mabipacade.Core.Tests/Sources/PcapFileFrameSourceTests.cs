using Mabipacade.Core.Sources;

namespace Mabipacade.Core.Tests.Sources;

public class PcapFileFrameSourceTests
{
    private const string FixturePath = "fixtures/tiny.pcap";

    [Fact(Skip = "Requires local fixture")]
    public async Task EmitsFrames_FromPcap()
    {
        if (!File.Exists(FixturePath)) return;

        int frameCount = 0;
        bool eosFired = false;
        using var src = new PcapFileFrameSource(FixturePath);
        src.FrameReceived += (_, _) => Interlocked.Increment(ref frameCount);
        src.EndOfStream += (_, _) => eosFired = true;

        await src.StartAsync(CancellationToken.None);
        await src.StopAsync();

        Assert.True(frameCount > 0);
        Assert.True(eosFired);
    }
}
