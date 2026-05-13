namespace Mabipacade.Core.Model;

internal sealed record MabiPacketSlice(
    ushort Op,
    ulong EntityId,
    byte[] Body);
