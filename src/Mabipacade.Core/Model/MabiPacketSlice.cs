namespace Mabipacade.Core.Model;

internal sealed record MabiPacketSlice(
    uint Op,
    ulong EntityId,
    byte[] Body);
