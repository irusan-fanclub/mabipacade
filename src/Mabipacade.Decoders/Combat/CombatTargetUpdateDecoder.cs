using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Combat;

public sealed record CombatTargetUpdate(ulong TargetId);

public sealed class CombatTargetUpdateDecoder : IPacketDecoder
{
    public uint Op => 0x791A;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        ulong target = e.Count > 0 && e[0].Type == MessageElemType.Long ? e[0].AsUInt64() : 0;
        return new CombatTargetUpdate(target);
    }
}
