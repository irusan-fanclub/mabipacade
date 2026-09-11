using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Misc;

// The party member whose HP/MP bar updates is carried by the packet's routing
// EntityId; TW's body is a 2-elem {Short, Int} (Aura sent the EID in the body
// plus four flag bytes). Observed values are 0, but we surface them so the
// record isn't empty.
public sealed record PartyWindowUpdate(ulong EntityId, ushort Value1, uint Value2);

public sealed class PartyWindowUpdateDecoder : IPacketDecoder
{
    public uint Op => 0x0000A43C;
    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        ushort v1 = e.Count > 0 && e[0].Type == MessageElemType.Short ? e[0].AsUInt16() : (ushort)0;
        uint v2 = e.Count > 1 && e[1].Type == MessageElemType.Int ? e[1].AsUInt32() : 0;
        return new PartyWindowUpdate(input.EntityId, v1, v2);
    }
}
