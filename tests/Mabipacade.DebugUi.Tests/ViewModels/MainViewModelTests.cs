using Mabipacade.DebugUi.Tests.Fakes;
using Mabipacade.DebugUi.ViewModels;

namespace Mabipacade.DebugUi.Tests.ViewModels;

public class MainViewModelTests
{
    [Fact]
    public void Initial_IsStoppedAndNotRunning()
    {
        var vm = new MainViewModel(new ImmediateDispatcher());
        Assert.False(vm.IsRunning);
        Assert.StartsWith("○ Stopped", vm.ActivityState);
    }

    [Fact(Skip = "Environment-dependent — depends on Mabinogi not running")]
    public void StartLive_BootstrapFails_StateBecomesStoppedWithReason()
    {
        // Without Mabinogi running, LiveSessionFactory.Create should throw BootstrapException.
        // MainViewModel.StartLive catches it and sets a stopped-with-reason state.
        var vm = new MainViewModel(new ImmediateDispatcher());
        vm.StartLive();
        Assert.False(vm.IsRunning);
        Assert.Contains("bootstrap", vm.ActivityState, StringComparison.OrdinalIgnoreCase);
    }
}
