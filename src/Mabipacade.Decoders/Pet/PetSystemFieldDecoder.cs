using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Pet;

// 18-elem stub, all fields 0 across samples except idx5 Byte (enable flag, always 1)
// and idx10 Byte (the only per-pet varying field, 0/1). Capture those two.
public sealed record PetSystemField(byte Flag1, byte Flag2);

public sealed class PetSystemFieldDecoder : IPacketDecoder
{
    public uint Op => 0x0000909A;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        byte flag1 = e.Count > 5 && e[5].Type == MessageElemType.Byte ? e[5].AsByte() : (byte)0;
        byte flag2 = e.Count > 10 && e[10].Type == MessageElemType.Byte ? e[10].AsByte() : (byte)0;
        return new PetSystemField(flag1, flag2);
    }
}
