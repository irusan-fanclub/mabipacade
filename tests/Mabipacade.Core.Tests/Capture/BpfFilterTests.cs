using System.Net;
using Mabipacade.Core.Capture;

namespace Mabipacade.Core.Tests.Capture;

public class BpfFilterTests
{
    private static TcpConnectionRow Row(string remote, ushort port, int pid = 4812) =>
        new(IPAddress.Parse("192.168.1.50"), 50000, IPAddress.Parse(remote), port,
            TcpConnectionState.Established, pid);

    [Fact]
    public void BuildsAReceiveDirectionFilter_FromTheServerNetwork()
    {
        var filter = BpfFilter.ForConnections(new[] { Row("210.208.80.41", 11022) });
        Assert.Equal("tcp and (src net 210.208.80.0/24)", filter);
    }

    [Fact]
    public void CoversTheWholeNetwork_SoAChannelSwitchNeedsNoUpdate()
    {
        // Channel servers move within one /24 — .41 and .34 have both been seen
        // on port 11022 — so covering the network keeps a switch gap-free.
        var filter = BpfFilter.ForConnections(new[]
        {
            Row("210.208.80.41", 11022),
            Row("210.208.80.34", 11022),
            Row("210.208.80.6", 11000),
        });
        Assert.Equal("tcp and (src net 210.208.80.0/24)", filter);
    }

    [Fact]
    public void OrdersNetworksDeterministically_SoAnUnchangedSetDoesNotRewriteTheFilter()
    {
        var a = BpfFilter.ForConnections(new[] { Row("54.238.121.9", 11022), Row("210.208.80.41", 11022) });
        var b = BpfFilter.ForConnections(new[] { Row("210.208.80.41", 11022), Row("54.238.121.9", 11022) });

        Assert.Equal(a, b);
        Assert.Equal("tcp and (src net 210.208.80.0/24 or src net 54.238.121.0/24)", a);
    }

    [Fact]
    public void SkipsWebPorts_SoCdnDownloadsAreNotCaptured()
    {
        var filter = BpfFilter.ForConnections(new[]
        {
            Row("18.166.193.141", 443),
            Row("13.35.0.1", 80),
            Row("210.208.80.41", 11022),
        });
        Assert.Equal("tcp and (src net 210.208.80.0/24)", filter);
    }

    [Fact]
    public void IgnoresConnectionsThatAreNotEstablished()
    {
        var rows = new[]
        {
            Row("210.208.80.41", 11022) with { State = TcpConnectionState.TimeWait },
            Row("54.238.121.9", 11022),
        };
        Assert.Equal("tcp and (src net 54.238.121.0/24)", BpfFilter.ForConnections(rows));
    }

    [Fact]
    public void ReturnsNull_WhenNothingQualifies()
    {
        // Null means "no opinion"; the caller keeps whatever filter is in place
        // rather than installing one that matches nothing.
        Assert.Null(BpfFilter.ForConnections(Array.Empty<TcpConnectionRow>()));
        Assert.Null(BpfFilter.ForConnections(new[] { Row("18.166.193.141", 443) }));
    }

    [Fact]
    public void ForAddress_CoversThatServersNetwork()
    {
        Assert.Equal("tcp and (src net 210.208.80.0/24)",
            BpfFilter.ForAddress(IPAddress.Parse("210.208.80.41")));
        Assert.Null(BpfFilter.ForAddress(IPAddress.Parse("::1")));
    }

    [Fact]
    public void BothDirections_WidensEachTermToEitherDirection()
    {
        Assert.Equal("tcp and (net 210.208.80.0/24)",
            BpfFilter.ForConnections(new[] { Row("210.208.80.41", 11022) }, bothDirections: true));
        Assert.Equal("tcp and (net 210.208.80.0/24)",
            BpfFilter.ForAddress(IPAddress.Parse("210.208.80.41"), bothDirections: true));
    }

    [Fact]
    public void IgnoresIpv6_WhichHasNoIpv4Network()
    {
        var rows = new[]
        {
            new TcpConnectionRow(IPAddress.IPv6Loopback, 50000,
                IPAddress.Parse("::1"), 11022, TcpConnectionState.Established, 4812),
            Row("210.208.80.41", 11022),
        };
        Assert.Equal("tcp and (src net 210.208.80.0/24)", BpfFilter.ForConnections(rows));
    }
}
