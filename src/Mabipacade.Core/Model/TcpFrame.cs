using System.Net;

namespace Mabipacade.Core.Model;

internal sealed record TcpFrame(
    IPAddress SrcIp,
    ushort SrcPort,
    IPAddress DstIp,
    ushort DstPort,
    uint SequenceNumber,
    byte[] Payload,
    DateTime TimestampUtc);
