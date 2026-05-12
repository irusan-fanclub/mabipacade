namespace Mabipacade.DebugUi.Services;

public interface IUiDispatcher
{
    void Invoke(Action action);
    void BeginInvoke(Action action);
}
