namespace Mabipacade.Core.Capture;

public interface ITcpConnectionTable
{
    IReadOnlyList<TcpConnectionRow> GetConnections();
}
