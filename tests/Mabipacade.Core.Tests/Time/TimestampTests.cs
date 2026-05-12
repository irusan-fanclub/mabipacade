using Mabipacade.Core.Time;

namespace Mabipacade.Core.Tests.Time;

public class TimestampTests
{
    [Fact]
    public void ToIso8601_UsesUtcWithMillis()
    {
        var t = new DateTime(2026, 5, 13, 8, 23, 11, 842, DateTimeKind.Utc);
        Assert.Equal("2026-05-13T08:23:11.842Z", Timestamp.ToIso8601(t));
    }

    [Fact]
    public void FromPcapTimeval_ReturnsUtc()
    {
        // 2026-05-13T08:23:11.842Z = 1778060591.842 unix seconds
        var dt = Timestamp.FromUnixSeconds(1778060591L, 842_000);
        Assert.Equal(DateTimeKind.Utc, dt.Kind);
        Assert.Equal(2026, dt.Year);
        Assert.Equal(842, dt.Millisecond);
    }

    [Fact]
    public void ToIso8601_RejectsLocalTime()
    {
        var local = DateTime.SpecifyKind(new DateTime(2026, 5, 13), DateTimeKind.Local);
        Assert.Throws<ArgumentException>(() => Timestamp.ToIso8601(local));
    }
}
