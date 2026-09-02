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
    public void ToLocalIso8601_ReadsAsLocalTime_AndKeepsTheInstant()
    {
        // Logs are read by a person against their own clock, so the rendered
        // time is local — but the offset stays on it, because a log outgrows the
        // machine that wrote it and a bare local time is then unreadable.
        var utc = new DateTime(2026, 8, 14, 8, 58, 22, 854, DateTimeKind.Utc);

        var text = Timestamp.ToLocalIso8601(utc);
        var parsed = DateTimeOffset.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(utc, parsed.UtcDateTime);
        Assert.Equal(TimeZoneInfo.Local.GetUtcOffset(utc), parsed.Offset);
        Assert.Equal(new DateTimeOffset(utc).ToLocalTime().Hour, parsed.Hour);
    }

    [Fact]
    public void ToLocalIso8601_TreatsUnspecifiedKindAsUtc()
    {
        // DateTime.ToLocalTime() reads an Unspecified value as already-local and
        // returns it untouched, which would silently emit the wrong instant for
        // any timestamp that lost its Kind along the way.
        var unspecified = new DateTime(2026, 8, 14, 8, 58, 22, 854, DateTimeKindUnspecified);
        var utc = DateTime.SpecifyKind(unspecified, DateTimeKind.Utc);

        Assert.Equal(Timestamp.ToLocalIso8601(utc), Timestamp.ToLocalIso8601(unspecified));
    }

    private const DateTimeKind DateTimeKindUnspecified = DateTimeKind.Unspecified;

    [Fact]
    public void ToIso8601_RejectsLocalTime()
    {
        var local = DateTime.SpecifyKind(new DateTime(2026, 5, 13), DateTimeKind.Local);
        Assert.Throws<ArgumentException>(() => Timestamp.ToIso8601(local));
    }
}
