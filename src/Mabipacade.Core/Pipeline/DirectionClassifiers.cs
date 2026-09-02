using System.Net;
using Mabipacade.Core.Capture;
using Mabipacade.Core.Model;

namespace Mabipacade.Core.Pipeline;

/// <summary>Decides a frame's direction from its TCP endpoints.</summary>
public delegate Direction DirectionClassifier(IPAddress srcIp, ushort srcPort, IPAddress dstIp, ushort dstPort);

public static class DirectionClassifiers
{
    /// <summary>
    /// Labels everything inbound — correct for a capture whose filter only
    /// admits server→client frames, which is the default capture mode.
    /// </summary>
    public static DirectionClassifier InboundOnly { get; } = (_, _, _, _) => Direction.Inbound;

    /// <summary>
    /// Classifies by which end looks like the region's game server: a frame
    /// addressed TO a server endpoint is the client talking, so outbound.
    /// Anything ambiguous (both or neither end matching — a profile with no
    /// port knowledge matches everything) stays inbound, which keeps existing
    /// inbound-only captures labelled exactly as before.
    /// </summary>
    public static DirectionClassifier ByServerEndpoint(RegionProfile region) =>
        (srcIp, srcPort, dstIp, dstPort) =>
            region.Contains(dstIp, dstPort) && !region.Contains(srcIp, srcPort)
                ? Direction.Outbound
                : Direction.Inbound;
}
