using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Misc;

// 0x5BCF (23 B): observed single {Byte flag}.
public sealed record ItemUnk5BCF(byte Flag);

public sealed class ItemUnk5BCFDecoder : IPacketDecoder
{
    public uint Op => 0x5BCF;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        byte flag = e.Count > 0 && e[0].Type == MessageElemType.Byte ? e[0].AsByte() : (byte)0;
        return new ItemUnk5BCF(flag);
    }
}
