using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Stats;

// 0x7533: observed {Long entityId, Byte flag}. 32 B fixed.
public sealed record StatUpdateUnk7533(ulong EntityId, byte Flag);

public sealed class StatUpdateUnk7533Decoder : IPacketDecoder
{
    public uint Op => 0x7533;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        ulong entityId = e.Count > 0 && e[0].Type == MessageElemType.Long ? e[0].AsUInt64() : 0;
        byte flag = e.Count > 1 && e[1].Type == MessageElemType.Byte ? e[1].AsByte() : (byte)0;
        return new StatUpdateUnk7533(entityId, flag);
    }
}
