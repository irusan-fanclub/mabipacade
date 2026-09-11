namespace Mabipacade.Core.Model;

public sealed record MabiPacket(
    DateTime TimestampUtc,
    Direction Direction,
    uint Op,
    ulong EntityId,
    IReadOnlyList<MessageElem> Elems,
    object? Decoded)
{
    /// <summary>Raw message body bytes after op + entityId. Set by the pipeline; null when constructed directly without a body.</summary>
    public byte[]? Body { get; init; }
}
