using Mabipacade.Core.Capture;

namespace Mabipacade.Core.Tests.Capture;

public class ProcessFinderTests
{
    [Fact]
    public void Find_ReturnsNull_WhenProcessAbsent()
    {
        var finder = new ProcessFinder();
        Assert.Null(finder.Find("DefinitelyNotARealProcess_xyz"));
    }

    [Fact]
    public void Find_ReturnsPid_ForCurrentProcess()
    {
        var finder = new ProcessFinder();
        var name = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
        var pid = finder.Find(name);
        Assert.NotNull(pid);
    }
}
