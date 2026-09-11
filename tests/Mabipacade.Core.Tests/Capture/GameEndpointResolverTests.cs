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
    public void Prefers_RegionMatch_AmongPidOwned_WhenNonGamePortSortsFirst()
    {
        // Repro of the live incident: Client.exe owns an auth connection
        // (210.208.80.10:8004) that enumerates before the channel server
        // (210.208.80.34:11022). Only the channel port matches the region.
        var tbl = new FakeTcpTable();
        tbl.Rows.Add(Row("18.166.193.141", 443, pid: 4812));
        tbl.Rows.Add(Row("210.208.80.10", 8004, pid: 4812));
        tbl.Rows.Add(Row("210.208.80.34", 11022, pid: 4812));
        var region = new RegionProfile("test",
            Array.Empty<IpRange>(),
            Array.Empty<ushort>(),
            new[] { new PortRange(11000, 11999) });
        var resolver = new GameEndpointResolver(tbl, processPid: 4812, region: region);
        var ep = resolver.TryResolveOnce();
        Assert.NotNull(ep);
        Assert.Equal(IPAddress.Parse("210.208.80.34"), ep!.RemoteAddress);
        Assert.Equal((ushort)11022, ep.RemotePort);
    }

    [Fact]
    public void FallsBack_ToAnyPidOwned_WhenNoneMatchRegion()
    {
        var tbl = new FakeTcpTable();
        tbl.Rows.Add(Row("210.208.80.10", 8004, pid: 4812));
        var region = new RegionProfile("test",
            Array.Empty<IpRange>(),
            Array.Empty<ushort>(),
            new[] { new PortRange(11000, 11999) });
        var resolver = new GameEndpointResolver(tbl, processPid: 4812, region: region);
        var ep = resolver.TryResolveOnce();
        Assert.NotNull(ep);
        Assert.Equal((ushort)8004, ep!.RemotePort);
    }

    [Fact]
    public void Prefers_HighestPort_WhenAcceleratorMovesGameOffRegionPorts()
    {
        // A network accelerator rewrites the destination, so neither the TW
        // channel port range nor any IP range matches. Table order would pick
        // the auxiliary stream; the game shard is the high port — the same
        // login-low / shard-high split that holds unaccelerated.
        var tbl = new FakeTcpTable();
        tbl.Rows.Add(Row("18.166.193.141", 443, pid: 4812));
        tbl.Rows.Add(Row("203.107.32.100", 9002, pid: 4812));
        tbl.Rows.Add(Row("203.107.32.100", 16888, pid: 4812));
        var region = new RegionProfile("test",
            Array.Empty<IpRange>(),
            Array.Empty<ushort>(),
            new[] { new PortRange(11000, 11999) });
        var resolver = new GameEndpointResolver(tbl, processPid: 4812, region: region);
        var ep = resolver.TryResolveOnce();
        Assert.NotNull(ep);
        Assert.Equal((ushort)16888, ep!.RemotePort);
    }

    [Fact]
    public void RegionMatch_OutranksHigherNonRegionPort()
    {
        // Guards the demotion of the region profile to a ranking hint: it
        // must still beat a higher port when it does match, so an unrelated
        // high-port stream can't outbid the real channel server.
        var tbl = new FakeTcpTable();
        tbl.Rows.Add(Row("1.2.3.4", 59999, pid: 4812));
        tbl.Rows.Add(Row("210.208.80.34", 11022, pid: 4812));
        var region = new RegionProfile("test",
            Array.Empty<IpRange>(),
            Array.Empty<ushort>(),
            new[] { new PortRange(11000, 11999) });
        var resolver = new GameEndpointResolver(tbl, processPid: 4812, region: region);
        var ep = resolver.TryResolveOnce();
        Assert.NotNull(ep);
        Assert.Equal((ushort)11022, ep!.RemotePort);
    }

    [Fact]
    public void Prefers_ShardOverLogin_WhenPidUnknownAndBothMatchRegion()
    {
        // Client.exe wasn't found, so the region profile is the only filter
        // and both the login server and the shard fall inside it. During the
        // few seconds of a channel switch both sockets are open at once —
        // table order must not decide which one the capture follows.
        var tbl = new FakeTcpTable();
        tbl.Rows.Add(Row("210.208.80.6", 11000, pid: 9999));
        tbl.Rows.Add(Row("210.208.80.34", 11022, pid: 9999));
        var region = new RegionProfile("test",
            Array.Empty<IpRange>(),
            Array.Empty<ushort>(),
            new[] { new PortRange(11000, 11999) });
        var resolver = new GameEndpointResolver(tbl, processPid: null, region: region);
        var ep = resolver.TryResolveOnce();
        Assert.NotNull(ep);
        Assert.Equal((ushort)11022, ep!.RemotePort);
    }

    [Fact]
    public void Returns_Null_WhenNothingMatches()
    {
        var tbl = new FakeTcpTable();
        var resolver = new GameEndpointResolver(tbl, processPid: 4812, region: null);
        Assert.Null(resolver.TryResolveOnce());
    }
}
