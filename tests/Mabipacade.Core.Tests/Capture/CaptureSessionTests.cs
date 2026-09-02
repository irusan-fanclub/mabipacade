using System.Net;
using Mabipacade.Core.Capture;
using Mabipacade.Core.Diagnostics;

namespace Mabipacade.Core.Tests.Capture;

public class CaptureSessionTests
{
    private sealed class FakeTable : ITcpConnectionTable
    {
        public List<TcpConnectionRow> Rows { get; } = new();
        public IReadOnlyList<TcpConnectionRow> GetConnections() => Rows;
    }

    private static TcpConnectionRow Row(string ip, ushort port, int pid)
        => new(IPAddress.Loopback, 50000, IPAddress.Parse(ip), port, TcpConnectionState.Established, pid);

    [Fact]
    public void EmitsLost_WhenEndpointDisappears()
    {
        var tbl = new FakeTable();
        tbl.Rows.Add(Row("61.218.1.2", 11000, pid: 4812));
        var session = new CaptureSession(tbl, processPid: 4812, region: null);
        var events = new List<SessionEvent>();
        session.SessionEventReceived += (_, e) => events.Add(e);

        session.PollOnce();
        tbl.Rows.Clear();
        session.PollOnce();

        Assert.Contains(events, e => e is SessionEvent.ConnectionEstablished);
        Assert.Contains(events, e => e is SessionEvent.ConnectionLost);
    }

    [Fact]
    public void EmitsResumed_SameAsLastTrue_WhenSameEndpointReturns()
    {
        var tbl = new FakeTable();
        var session = new CaptureSession(tbl, processPid: 4812, region: null);
        var events = new List<SessionEvent>();
        session.SessionEventReceived += (_, e) => events.Add(e);

        tbl.Rows.Add(Row("61.218.1.2", 11000, pid: 4812));
        session.PollOnce();
        tbl.Rows.Clear();
        session.PollOnce();
        tbl.Rows.Add(Row("61.218.1.2", 11000, pid: 4812));
        session.PollOnce();

        var resumed = events.OfType<SessionEvent.ConnectionResumed>().Single();
        Assert.True(resumed.SameAsLast);
    }

    [Fact]
    public void EmitsResumed_SameAsLastFalse_WhenNewEndpoint()
    {
        var tbl = new FakeTable();
        var session = new CaptureSession(tbl, processPid: 4812, region: null);
        var events = new List<SessionEvent>();
        session.SessionEventReceived += (_, e) => events.Add(e);

        tbl.Rows.Add(Row("61.218.1.2", 11000, pid: 4812));
        session.PollOnce();
        tbl.Rows.Clear();
        session.PollOnce();
        tbl.Rows.Add(Row("61.218.1.3", 11000, pid: 4812));
        session.PollOnce();

        var resumed = events.OfType<SessionEvent.ConnectionResumed>().Single();
        Assert.False(resumed.SameAsLast);
    }

    // --- watchdog: keeping the capture filter current ---------------------

    [Fact]
    public void AppliesAFilter_ForTheClientsServerNetwork()
    {
        var tbl = new FakeTable();
        tbl.Rows.Add(Row("210.208.80.41", 11022, pid: 4812));
        var applied = new List<string>();
        var session = new CaptureSession(tbl, processPid: 4812, region: null, applyFilter: applied.Add);

        session.PollOnce();

        Assert.Equal("tcp and (src net 210.208.80.0/24)", Assert.Single(applied));
    }

    [Fact]
    public void DoesNotReapplyAnUnchangedFilter()
    {
        // Every apply is a pcap_setfilter on a live handle; repeating it each
        // poll would be pointless churn on the capture.
        var tbl = new FakeTable();
        tbl.Rows.Add(Row("210.208.80.41", 11022, pid: 4812));
        var applied = new List<string>();
        var session = new CaptureSession(tbl, processPid: 4812, region: null, applyFilter: applied.Add);

        session.PollOnce();
        session.PollOnce();
        // A channel switch inside the same network changes nothing.
        tbl.Rows.Add(Row("210.208.80.34", 11022, pid: 4812));
        session.PollOnce();

        Assert.Single(applied);
    }

    [Fact]
    public void WidensTheFilter_WhenTheClientReachesANewNetwork()
    {
        // An accelerator retargeting mid-session, or a server outside the
        // network we started on.
        var tbl = new FakeTable();
        tbl.Rows.Add(Row("210.208.80.41", 11022, pid: 4812));
        var applied = new List<string>();
        var session = new CaptureSession(tbl, processPid: 4812, region: null, applyFilter: applied.Add);

        session.PollOnce();
        tbl.Rows.Add(Row("54.238.121.9", 11022, pid: 4812));
        session.PollOnce();

        Assert.Equal(2, applied.Count);
        Assert.Equal("tcp and (src net 210.208.80.0/24 or src net 54.238.121.0/24)", applied[1]);
    }

    [Fact]
    public void KeepsTheCurrentFilter_WhenNothingQualifies()
    {
        // Installing a filter that matches nothing would be worse than keeping
        // one that is merely stale.
        var tbl = new FakeTable();
        tbl.Rows.Add(Row("210.208.80.41", 11022, pid: 4812));
        var applied = new List<string>();
        var session = new CaptureSession(tbl, processPid: 4812, region: null, applyFilter: applied.Add);

        session.PollOnce();
        tbl.Rows.Clear();
        session.PollOnce();

        Assert.Single(applied);
    }

    [Fact]
    public void SurvivesAFailingApply_AndRetriesOnTheNextPoll()
    {
        var tbl = new FakeTable();
        tbl.Rows.Add(Row("210.208.80.41", 11022, pid: 4812));
        int attempts = 0;
        var session = new CaptureSession(tbl, processPid: 4812, region: null,
            applyFilter: _ => { attempts++; throw new InvalidOperationException("pcap_setfilter failed"); });

        session.PollOnce();   // must not propagate
        session.PollOnce();

        Assert.Equal(2, attempts);
    }

    [Fact]
    public void SurvivesAFailingTable()
    {
        var tbl = new ThrowingTable();
        var session = new CaptureSession(tbl, processPid: 4812, region: null, applyFilter: _ => { });
        session.PollOnce();   // must not propagate
    }

    private sealed class ThrowingTable : ITcpConnectionTable
    {
        public IReadOnlyList<TcpConnectionRow> GetConnections() =>
            throw new InvalidOperationException("GetExtendedTcpTable rc=1");
    }
}
