using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Pet;

public sealed record TelePetR(byte Success, ulong PetId);

public sealed class TelePetRDecoder : IPacketDecoder
{
    public uint Op => 0x9034;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        byte success = e.Count > 0 && e[0].Type == MessageElemType.Byte ? e[0].AsByte() : (byte)0;
        ulong pet = e.Count > 1 && e[1].Type == MessageElemType.Long ? e[1].AsUInt64() : 0;
        return new TelePetR(success, pet);
    }
}
