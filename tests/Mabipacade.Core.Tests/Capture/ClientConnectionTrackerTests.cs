using System.Net;
using Mabipacade.Core.Capture;

namespace Mabipacade.Core.Tests.Capture;

public class ClientConnectionTrackerTests
{
    private const int ClientPid = 4812;
    private const int OtherPid = 9999;

    private sealed class CountingTable : ITcpConnectionTable
    {
        private readonly Func<IReadOnlyList<TcpConnectionRow>> _rows;
        public int Queries { get; private set; }

        public CountingTable(Func<IReadOnlyList<TcpConnectionRow>> rows) => _rows = rows;

        public IReadOnlyList<TcpConnectionRow> GetConnections()
        {
            Queries++;
            return _rows();
        }
    }

    private static TcpConnectionRow Row(ushort localPort, int pid, ushort remotePort = 11022) =>
        new(IPAddress.Parse("192.168.1.50"), localPort,
            IPAddress.Parse("210.208.80.41"), remotePort,
            TcpConnectionState.Established, pid);

    private sealed class FakeClock
    {
        public DateTime Now { get; set; } = new(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc);
        public DateTime Read() => Now;
    }

    [Fact]
    public void AcceptsAPortOwnedByTheClient()
    {
        var table = new CountingTable(() => new[] { Row(55588, ClientPid) });
        var tracker = new ClientConnectionTracker(table, ClientPid);

        Assert.True(tracker.IsClientLocalPort(55588));
    }

    [Fact]
    public void RejectsAPortOwnedByAnotherProcess()
    {
        // The reported case: a NAT'd VM's traffic leaves through the hypervisor's
        // service, sharing our address but not our port.
        var table = new CountingTable(() => new[] { Row(55588, ClientPid), Row(61000, OtherPid) });
        var tracker = new ClientConnectionTracker(table, ClientPid);

        Assert.False(tracker.IsClientLocalPort(61000));
    }

    [Fact]
    public void RepeatedHits_QueryTheTableOnce()
    {
        // The decorator asks once per new stream, but a burst of new streams
        // must not turn into a burst of syscalls.
        var table = new CountingTable(() => new[] { Row(55588, ClientPid) });
        var tracker = new ClientConnectionTracker(table, ClientPid);

        for (int i = 0; i < 10; i++) Assert.True(tracker.IsClientLocalPort(55588));
        Assert.Equal(1, table.Queries);
    }

    [Fact]
    public void AMiss_RePollsOnce_SoANewSocketIsAdmittedImmediately()
    {
        // A channel switch opens a socket between scheduled polls. Waiting for
        // the next one would drop the snapshot the server sends on connect.
        var rows = new List<TcpConnectionRow> { Row(55588, ClientPid) };
        var table = new CountingTable(() => rows.ToList());
        var clock = new FakeClock();
        var tracker = new ClientConnectionTracker(table, ClientPid, clock: clock.Read);

        Assert.True(tracker.IsClientLocalPort(55588));
        Assert.Equal(1, table.Queries);

        rows.Add(Row(59599, ClientPid));                 // the switch happens
        Assert.True(tracker.IsClientLocalPort(59599));   // admitted without waiting for the next poll
        Assert.Equal(2, table.Queries);
    }

    [Fact]
    public void EveryMissRePolls_WhichIsWhyCallersMustCachePerConnection()
    {
        // Deliberate: an unknown port is re-checked against a fresh table so a
        // channel switch is admitted at once. The cost is that a miss is always
        // a syscall, which is why the frame filter decides once per stream
        // rather than calling this per frame.
        var table = new CountingTable(() => new[] { Row(55588, ClientPid) });
        var tracker = new ClientConnectionTracker(table, ClientPid);

        Assert.False(tracker.IsClientLocalPort(61000));
        Assert.False(tracker.IsClientLocalPort(61000));
        Assert.Equal(2, table.Queries);
    }

    [Fact]
    public void TheCacheExpires()
    {
        var rows = new List<TcpConnectionRow> { Row(55588, ClientPid) };
        var table = new CountingTable(() => rows.ToList());
        var clock = new FakeClock();
        var tracker = new ClientConnectionTracker(table, ClientPid,
            cacheMaxAge: TimeSpan.FromSeconds(2), clock: clock.Read);

        Assert.True(tracker.IsClientLocalPort(55588));
        Assert.Equal(1, table.Queries);

        clock.Now = clock.Now.AddSeconds(3);
        Assert.True(tracker.IsClientLocalPort(55588));
        Assert.Equal(2, table.Queries);
    }

    [Fact]
    public void FailsOpen_WhenTheTableCannotBeRead()
    {
        // Losing game data is worse than recording a little extra, so an
        // unreadable table admits the frame.
        var table = new CountingTable(() => throw new InvalidOperationException("GetExtendedTcpTable rc=1"));
        var tracker = new ClientConnectionTracker(table, ClientPid);

        Assert.True(tracker.IsClientLocalPort(61000));
    }

    [Fact]
    public void WithoutAKnownPid_AcceptsEverything()
    {
        // The process was not found, so there is nothing to compare against and
        // no basis for rejecting anything.
        var table = new CountingTable(() => new[] { Row(61000, OtherPid) });
        var tracker = new ClientConnectionTracker(table, processId: null);

        Assert.True(tracker.IsClientLocalPort(61000));
    }

    [Fact]
    public void Snapshot_ReturnsOnlyTheClientsConnections()
    {
        var table = new CountingTable(() => new[]
        {
            Row(55588, ClientPid),
            Row(61000, OtherPid),
            Row(55590, ClientPid, remotePort: 8004),
        });
        var tracker = new ClientConnectionTracker(table, ClientPid);

        var rows = tracker.Snapshot();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(ClientPid, r.OwningPid));
    }

    [Fact]
    public void Snapshot_IsEmpty_WhenTheTableCannotBeRead()
    {
        var table = new CountingTable(() => throw new InvalidOperationException("boom"));
        var tracker = new ClientConnectionTracker(table, ClientPid);

        // Empty, not an exception: the watchdog reads this on a timer and an
        // empty result makes it keep the filter it already has.
        Assert.Empty(tracker.Snapshot());
    }
}
