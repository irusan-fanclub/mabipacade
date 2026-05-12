using System.Net;

namespace Mabipacade.Core.Capture;

public sealed record GameEndpoint(
    int? ProcessId,
    IPAddress RemoteAddress,
    ushort RemotePort,
    IPAddress LocalAddress,
    ushort LocalPort);
