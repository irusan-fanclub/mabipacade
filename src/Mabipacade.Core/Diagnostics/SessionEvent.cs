using System.Net;

namespace Mabipacade.Core.Diagnostics;

public abstract record SessionEvent(DateTime TimestampUtc)
{
    public sealed record SessionStart(DateTime TimestampUtc, string Region, int? ProcessId)
        : SessionEvent(TimestampUtc);
    public sealed record SessionEnd(DateTime TimestampUtc, string Reason)
        : SessionEvent(TimestampUtc);
    public sealed record ConnectionEstablished(DateTime TimestampUtc, IPEndPoint Remote, string NicName)
        : SessionEvent(TimestampUtc);
    public sealed record ConnectionLost(DateTime TimestampUtc, IPEndPoint LastRemote)
        : SessionEvent(TimestampUtc);
    public sealed record ConnectionResumed(DateTime TimestampUtc, IPEndPoint NewRemote, bool SameAsLast)
        : SessionEvent(TimestampUtc);
    public sealed record FrameResync(DateTime TimestampUtc, long ByteOffset, string Reason)
        : SessionEvent(TimestampUtc);
    public sealed record BadBody(DateTime TimestampUtc, ushort Op, int Length)
        : SessionEvent(TimestampUtc);
    public sealed record DecoderFailed(DateTime TimestampUtc, ushort Op, string ExceptionMessage)
        : SessionEvent(TimestampUtc);
}
