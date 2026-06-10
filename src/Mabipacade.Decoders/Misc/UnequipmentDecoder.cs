using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Misc;

// 0x59E7 Unequipment: {Int pocket}.
public sealed record Unequipment(uint Pocket);

public sealed class UnequipmentDecoder : IPacketDecoder
{
    public uint Op => 0x000059E7;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        uint pocket = e.Count > 0 && e[0].Type == MessageElemType.Int ? e[0].AsUInt32() : 0;
        return new Unequipment(pocket);
    }
}
