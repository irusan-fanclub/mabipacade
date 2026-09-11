using System.Net;

namespace Mabipacade.Core.Capture;

public sealed record IpRange(IPAddress Start, IPAddress End)
{
    public bool Contains(IPAddress addr)
    {
        if (addr.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
        if (Start.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
        long a = ToLong(addr);
        return a >= ToLong(Start) && a <= ToLong(End);
    }
    private static long ToLong(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return ((long)b[0] << 24) | ((long)b[1] << 16) | ((long)b[2] << 8) | b[3];
    }
}

public sealed record PortRange(ushort Start, ushort End)
{
    public bool Contains(ushort port) => port >= Start && port <= End;
}

public sealed record RegionProfile(
    string Name,
    IReadOnlyList<IpRange> ServerRanges,
    IReadOnlyList<ushort> KnownPorts,
    IReadOnlyList<PortRange>? KnownPortRanges = null)
{
    public bool Contains(IPAddress addr, ushort port)
    {
        if (!MatchesPort(port)) return false;
        if (ServerRanges.Count == 0) return true;
        foreach (var r in ServerRanges)
            if (r.Contains(addr)) return true;
        return false;
    }

    private bool MatchesPort(ushort port)
    {
        bool hasFilter = KnownPorts.Count > 0 || KnownPortRanges is { Count: > 0 };
        if (!hasFilter) return true;
        if (KnownPorts.Contains(port)) return true;
        if (KnownPortRanges is not null)
            foreach (var r in KnownPortRanges)
                if (r.Contains(port)) return true;
        return false;
    }
}

public static class RegionProfiles
{
    // 11000 is the TW login server; channel servers sit above it (11022 observed live).
    public static RegionProfile Taiwan { get; } = new("tw",
        Array.Empty<IpRange>(),
        Array.Empty<ushort>(),
        new[] { new PortRange(11000, 11999) });
    public static RegionProfile Japan  { get; } = new("jp", Array.Empty<IpRange>(), Array.Empty<ushort>());
    public static RegionProfile Korea  { get; } = new("kr", Array.Empty<IpRange>(), Array.Empty<ushort>());
}
