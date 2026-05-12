using Mabipacade.Cli.Filters;
using Mabipacade.Core.Diagnostics;

namespace Mabipacade.Cli.Tests.Filters;

public class DiagnosticsLevelTests
{
    [Fact]
    public void Off_SuppressesDiagnosticEvents()
    {
        Assert.False(DiagnosticsLevel.Off.PassesLive(new SessionEvent.BadBody(DateTime.UtcNow, 0x6984, 10)));
        Assert.False(DiagnosticsLevel.Off.PassesLive(new SessionEvent.FrameResync(DateTime.UtcNow, 0, "x")));
        Assert.False(DiagnosticsLevel.Off.PassesLive(new SessionEvent.DecoderFailed(DateTime.UtcNow, 0x6984, "boom")));
    }

    [Fact]
    public void Off_PassesNonDiagnosticEvents()
    {
        Assert.True(DiagnosticsLevel.Off.PassesLive(new SessionEvent.SessionStart(DateTime.UtcNow, "tw", null)));
        Assert.True(DiagnosticsLevel.Off.PassesLive(new SessionEvent.ConnectionLost(DateTime.UtcNow,
            new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 11000))));
    }

    [Fact]
    public void On_PassesEverything()
    {
        Assert.True(DiagnosticsLevel.On.PassesLive(new SessionEvent.BadBody(DateTime.UtcNow, 0x6984, 10)));
    }

    [Fact]
    public void Parse_OffAndOn()
    {
        Assert.Equal(DiagnosticsLevel.Off, DiagnosticsLevel.Parse("off"));
        Assert.Equal(DiagnosticsLevel.On, DiagnosticsLevel.Parse("on"));
        Assert.Equal(DiagnosticsLevel.Off, DiagnosticsLevel.Parse(null));
    }

    [Fact]
    public void Parse_Summary_Throws_NotYetImplemented()
    {
        var ex = Assert.Throws<FormatException>(() => DiagnosticsLevel.Parse("summary"));
        Assert.Contains("not yet implemented", ex.Message);
    }

    [Fact]
    public void Parse_Invalid_Throws()
    {
        Assert.Throws<FormatException>(() => DiagnosticsLevel.Parse("loud"));
    }
}
