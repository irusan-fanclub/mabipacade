using Mabipacade.DebugUi.ViewModels;

namespace Mabipacade.DebugUi.Tests.ViewModels;

public class RelayCommandTests
{
    [Fact]
    public void Execute_RunsAction()
    {
        int n = 0;
        var cmd = new RelayCommand(_ => n++);
        cmd.Execute(null);
        Assert.Equal(1, n);
    }

    [Fact]
    public void CanExecute_DefaultsTrue()
    {
        var cmd = new RelayCommand(_ => { });
        Assert.True(cmd.CanExecute(null));
    }

    [Fact]
    public void CanExecute_RespectsPredicate()
    {
        bool allow = false;
        var cmd = new RelayCommand(_ => { }, _ => allow);
        Assert.False(cmd.CanExecute(null));
        allow = true;
        Assert.True(cmd.CanExecute(null));
    }

    [Fact]
    public void RaiseCanExecuteChanged_FiresEvent()
    {
        var cmd = new RelayCommand(_ => { });
        int n = 0;
        cmd.CanExecuteChanged += (_, _) => n++;
        cmd.RaiseCanExecuteChanged();
        Assert.Equal(1, n);
    }
}
