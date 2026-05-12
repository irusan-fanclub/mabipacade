using System.Net;

namespace Mabipacade.Core.Capture;

public sealed record IpRange(IPAddress Start, IPAddress End)
{
    public bool Contains(IPAddress addr)
    {
        long a = ToLong(addr);
        return a >= ToLong(Start) && a <= ToLong(End);
    }
    private static long ToLong(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return ((long)b[0] << 24) | ((long)b[1] << 16) | ((long)b[2] << 8) | b[3];
    }
}

public sealed record RegionProfile(
    string Name,
    IReadOnlyList<IpRange> ServerRanges,
    IReadOnlyList<ushort> KnownPorts)
{
    public bool Contains(IPAddress addr, ushort port)
    {
        if (KnownPorts.Count > 0 && !KnownPorts.Contains(port)) return false;
        if (ServerRanges.Count == 0) return true;
        foreach (var r in ServerRanges)
            if (r.Contains(addr)) return true;
        return false;
    }
}

public static class RegionProfiles
{
    public static RegionProfile Taiwan { get; } = new("tw",
        Array.Empty<IpRange>(),
        new ushort[] { 11000 });
    public static RegionProfile Japan  { get; } = new("jp", Array.Empty<IpRange>(), Array.Empty<ushort>());
    public static RegionProfile Korea  { get; } = new("kr", Array.Empty<IpRange>(), Array.Empty<ushort>());
}
