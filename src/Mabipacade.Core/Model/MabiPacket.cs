namespace Mabipacade.Core.Model;

public sealed record MabiPacket(
    DateTime TimestampUtc,
    Direction Direction,
    ushort Op,
    ulong EntityId,
    IReadOnlyList<MessageElem> Elems,
    object? Decoded);
