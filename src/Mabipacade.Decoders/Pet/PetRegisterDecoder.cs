using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Pet;

public sealed record PetRegister(ulong PetId, byte Flag);

public sealed class PetRegisterDecoder : IPacketDecoder
{
    public uint Op => 0x00009024;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        ulong pet = e.Count > 0 && e[0].Type == MessageElemType.Long ? e[0].AsUInt64() : 0;
        byte flag = e.Count > 1 && e[1].Type == MessageElemType.Byte ? e[1].AsByte() : (byte)0;
        return new PetRegister(pet, flag);
    }
}
