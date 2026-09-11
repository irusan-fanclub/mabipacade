using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Entity;

// TW body is { Long eid, Byte }. The Long is the disappearing entity's EID
// (props use 0x52D1 instead, without the byte). Observed byte is always 1;
// meaning unknown, surfaced as Value1.
public sealed record EntityDisappear(ulong EntityId, byte Value1);

public sealed class EntityDisappearDecoder : IPacketDecoder
{
    public uint Op => 0x0000520D;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        ulong eid = e.Count > 0 && e[0].Type == MessageElemType.Long ? e[0].AsUInt64() : 0UL;
        byte v1 = e.Count > 1 && e[1].Type == MessageElemType.Byte ? e[1].AsByte() : (byte)0;
        return new EntityDisappear(eid, v1);
    }
}
