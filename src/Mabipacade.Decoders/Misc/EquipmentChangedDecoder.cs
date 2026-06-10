using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Misc;

public sealed record EquipmentChanged;

public sealed class EquipmentChangedDecoder : IPacketDecoder
{
    public uint Op => 0x000059E6;
    public object Decode(DecoderInput input) => new EquipmentChanged();
}
