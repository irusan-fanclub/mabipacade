using System.Net;
using Mabipacade.Core.Capture;

namespace Mabipacade.Core.Tests.Capture;

public class GameEndpointResolverTests
{
    private sealed class FakeTcpTable : ITcpConnectionTable
    {
        public List<TcpConnectionRow> Rows { get; } = new();
        public IReadOnlyList<TcpConnectionRow> GetConnections() => Rows;
    }

    private static TcpConnectionRow Row(string remote, ushort port, int pid, TcpConnectionState state = TcpConnectionState.Established)
        => new(IPAddress.Loopback, 50000, IPAddress.Parse(remote), port, state, pid);

    [Fact]
    public void Resolves_FromPid_WhenSingleEstablished()
    {
        var tbl = new FakeTcpTable();
        tbl.Rows.Add(Row("61.218.1.2", 11000, pid: 4812));
        var resolver = new GameEndpointResolver(tbl, processPid: 4812, region: null);
        var ep = resolver.TryResolveOnce();
        Assert.NotNull(ep);
        Assert.Equal(IPAddress.Parse("61.218.1.2"), ep!.RemoteAddress);
        Assert.Equal((ushort)11000, ep.RemotePort);
    }

    [Fact]
    public void Prefers_NonWebPort_WhenMultipleEstablished()
    {
        var tbl = new FakeTcpTable();
        tbl.Rows.Add(Row("1.1.1.1", 443, pid: 4812));
        tbl.Rows.Add(Row("61.218.1.2", 11000, pid: 4812));
        var resolver = new GameEndpointResolver(tbl, processPid: 4812, region: null);
        var ep = resolver.TryResolveOnce();
        Assert.Equal((ushort)11000, ep!.RemotePort);
    }

    [Fact]
    public void Falls_BackToRegion_WhenPidUnknown()
    {
        var tbl = new FakeTcpTable();
        tbl.Rows.Add(Row("10.0.0.1", 11000, pid: 9999));
        var region = new RegionProfile("test",
            new[] { new IpRange(IPAddress.Parse("10.0.0.0"), IPAddress.Parse("10.0.0.255")) },
            new ushort[] { 11000 });
        var resolver = new GameEndpointResolver(tbl, processPid: null, region: region);
        var ep = resolver.TryResolveOnce();
        Assert.NotNull(ep);
        Assert.Equal((ushort)11000, ep!.RemotePort);
    }

    [Fact]
    public void Returns_Null_WhenNothingMatches()
    {
        var tbl = new FakeTcpTable();
        var resolver = new GameEndpointResolver(tbl, processPid: 4812, region: null);
        Assert.Null(resolver.TryResolveOnce());
    }
}
