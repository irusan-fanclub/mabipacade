namespace Mabipacade.Core.Time;

public static class Timestamp
{
    public static string ToIso8601(DateTime t)
    {
        if (t.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timestamp must be UTC", nameof(t));
        return t.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    }

    /// <summary>
    /// Renders a UTC instant as local time carrying its offset, e.g.
    /// <c>2026-08-14T16:58:22.8540000+08:00</c>.
    ///
    /// Local, because a log is read against the clock on the wall. With the
    /// offset, because the log outlives the machine that wrote it: a bare local
    /// time cannot be told from a UTC one, and neither can be placed on a
    /// timeline from somewhere else.
    /// </summary>
    public static string ToLocalIso8601(DateTime t)
    {
        // Pin the kind first. ToLocalTime() reads an Unspecified value as
        // already-local and hands it back unchanged, which would emit the wrong
        // instant for any timestamp that lost its Kind on the way here.
        var utc = DateTime.SpecifyKind(t, DateTimeKind.Utc);
        return new DateTimeOffset(utc).ToLocalTime()
            .ToString("O", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static DateTime FromUnixSeconds(long seconds, int microseconds)
    {
        return DateTime.UnixEpoch
            .AddSeconds(seconds)
            .AddTicks(microseconds * 10);
    }
}
