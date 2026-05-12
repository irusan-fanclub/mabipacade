namespace Mabipacade.Core.Time;

public static class Timestamp
{
    public static string ToIso8601(DateTime t)
    {
        if (t.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timestamp must be UTC", nameof(t));
        return t.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    }

    public static DateTime FromUnixSeconds(long seconds, int microseconds)
    {
        return DateTime.UnixEpoch
            .AddSeconds(seconds)
            .AddTicks(microseconds * 10);
    }
}
