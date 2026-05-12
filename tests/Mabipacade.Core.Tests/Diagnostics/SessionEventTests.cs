using System.Net;
using Mabipacade.Core.Diagnostics;

namespace Mabipacade.Core.Tests.Diagnostics;

public class SessionEventTests
{
    [Fact]
    public void ConnectionResumed_ExposesSameAsLastFlag()
    {
        var ts = DateTime.UtcNow;
        var ev = new SessionEvent.ConnectionResumed(ts, new IPEndPoint(IPAddress.Parse("1.2.3.4"), 11000), SameAsLast: false);
        Assert.Equal(ts, ev.TimestampUtc);
        Assert.False(ev.SameAsLast);
    }

    [Fact]
    public void PatternMatching_DistinguishesVariants()
    {
        SessionEvent ev = new SessionEvent.SessionStart(DateTime.UtcNow, "tw", 4812);
        var name = ev switch
        {
            SessionEvent.SessionStart => "start",
            SessionEvent.SessionEnd => "end",
            SessionEvent.ConnectionLost => "lost",
            _ => "other"
        };
        Assert.Equal("start", name);
    }
}
