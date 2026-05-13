using Mabipacade.Core.Diagnostics;
using Mabipacade.DebugUi.ViewModels;
using System.Net;

namespace Mabipacade.DebugUi.Tests.ViewModels;

public class StatusViewModelTests
{
    [Fact]
    public void Initial_StateIsDisconnected()
    {
        var vm = new StatusViewModel();
        Assert.Equal("disconnected", vm.ConnectionLabel);
    }

    [Fact]
    public void OnSessionEvent_Established_UpdatesLabel()
    {
        var vm = new StatusViewModel();
        vm.HandleEvent(new SessionEvent.ConnectionEstablished(DateTime.UtcNow,
            new IPEndPoint(IPAddress.Parse("61.218.1.2"), 11000), "eth0"));
        Assert.Equal("connected 61.218.1.2:11000", vm.ConnectionLabel);
    }

    [Fact]
    public void OnSessionEvent_Lost_UpdatesLabel()
    {
        var vm = new StatusViewModel();
        vm.HandleEvent(new SessionEvent.ConnectionLost(DateTime.UtcNow,
            new IPEndPoint(IPAddress.Parse("1.2.3.4"), 11000)));
        Assert.Equal("lost 1.2.3.4:11000", vm.ConnectionLabel);
    }

    [Fact]
    public void UpdateCounters_ReflectsTotals()
    {
        var vm = new StatusViewModel();
        vm.UpdateCounters(totalPackets: 1234, badBody: 5, framesPerSec: 200, bytesPerSec: 4096);
        Assert.Equal(1234, vm.TotalPackets);
        Assert.Equal(5, vm.BadBodyCount);
        Assert.Equal(200, vm.PacketsPerSec);
        Assert.Equal(4096, vm.BytesPerSec);
    }
}
