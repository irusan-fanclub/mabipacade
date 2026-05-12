using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Misc;

public sealed record EquipmentChanged;

public sealed class EquipmentChangedDecoder : IPacketDecoder
{
    public ushort Op => 0x59E6;
    public object Decode(DecoderInput input) => new EquipmentChanged();
}
