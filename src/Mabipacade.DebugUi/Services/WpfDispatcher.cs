using System.Windows.Threading;

namespace Mabipacade.DebugUi.Services;

internal sealed class WpfDispatcher : IUiDispatcher
{
    private readonly Dispatcher _dispatcher;
    public WpfDispatcher(Dispatcher dispatcher) { _dispatcher = dispatcher; }
    public void Invoke(Action action) => _dispatcher.Invoke(action);
    public void BeginInvoke(Action action) => _dispatcher.BeginInvoke(action);
}
