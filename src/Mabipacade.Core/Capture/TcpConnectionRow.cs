using System.Net;

namespace Mabipacade.Core.Capture;

public sealed record TcpConnectionRow(
    IPAddress LocalAddress,
    ushort LocalPort,
    IPAddress RemoteAddress,
    ushort RemotePort,
    TcpConnectionState State,
    int OwningPid);

public enum TcpConnectionState
{
    Unknown, Closed, Listen, SynSent, SynReceived, Established,
    FinWait1, FinWait2, CloseWait, Closing, LastAck, TimeWait, DeleteTcb
}
