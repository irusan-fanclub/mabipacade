using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Pet;

public sealed record SummonPetR(byte Active, ulong PetId);

public sealed class SummonPetRDecoder : IPacketDecoder
{
    public uint Op => 0x0000902D;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        byte active = e.Count > 0 && e[0].Type == MessageElemType.Byte ? e[0].AsByte() : (byte)0;
        ulong pet = e.Count > 1 && e[1].Type == MessageElemType.Long ? e[1].AsUInt64() : 0;
        return new SummonPetR(active, pet);
    }
}
