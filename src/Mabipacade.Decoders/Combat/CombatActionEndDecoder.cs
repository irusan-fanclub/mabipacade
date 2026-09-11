using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Combat;

public sealed record CombatActionEnd(uint ActionId);

public sealed class CombatActionEndDecoder : IPacketDecoder
{
    public uint Op => 0x00007925;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        uint actionId = e.Count > 0 && e[0].Type == MessageElemType.Int ? e[0].AsUInt32() : 0;
        return new CombatActionEnd(actionId);
    }
}
