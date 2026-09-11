using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Pet;

public sealed record PetEquipSlotInit(byte Slot, byte Flag, uint A, uint B);

public sealed class PetEquipSlotInitDecoder : IPacketDecoder
{
    public uint Op => 0x000090A7;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        byte slot = e.Count > 0 && e[0].Type == MessageElemType.Byte ? e[0].AsByte() : (byte)0;
        byte flag = e.Count > 1 && e[1].Type == MessageElemType.Byte ? e[1].AsByte() : (byte)0;
        uint a = e.Count > 2 && e[2].Type == MessageElemType.Int ? e[2].AsUInt32() : 0;
        uint b = e.Count > 3 && e[3].Type == MessageElemType.Int ? e[3].AsUInt32() : 0;
        return new PetEquipSlotInit(slot, flag, a, b);
    }
}
