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
}
