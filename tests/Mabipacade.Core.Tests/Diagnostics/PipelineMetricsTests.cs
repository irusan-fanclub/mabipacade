using Mabipacade.Core.Diagnostics;

namespace Mabipacade.Core.Tests.Diagnostics;

public class PipelineMetricsTests
{
    [Fact]
    public void Counters_StartAtZero()
    {
        var m = new PipelineMetrics();
        Assert.Equal(0, m.TotalFrames);
        Assert.Equal(0, m.TotalPackets);
        Assert.Equal(0, m.BadBodyCount);
        Assert.Equal(0, m.FrameResyncCount);
    }

    [Fact]
    public void Increments_AreThreadSafe()
    {
        var m = new PipelineMetrics();
        Parallel.For(0, 1000, _ => m.IncrementFrames());
        Assert.Equal(1000, m.TotalFrames);
    }
}
