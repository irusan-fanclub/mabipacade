using Mabipacade.DebugUi.Services;

namespace Mabipacade.DebugUi.Tests.Fakes;

internal sealed class ImmediateDispatcher : IUiDispatcher
{
    public void Invoke(Action action) => action();
    public void BeginInvoke(Action action) => action();
}
