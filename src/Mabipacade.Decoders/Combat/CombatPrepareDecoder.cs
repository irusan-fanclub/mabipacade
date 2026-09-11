using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Combat;

public sealed record CombatPrepare(byte Flag, ulong TargetId);

public sealed class CombatPrepareDecoder : IPacketDecoder
{
    public uint Op => 0x00007919;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        byte flag = e.Count > 0 && e[0].Type == MessageElemType.Byte ? e[0].AsByte() : (byte)0;
        ulong target = e.Count > 1 && e[1].Type == MessageElemType.Long ? e[1].AsUInt64() : 0;
        return new CombatPrepare(flag, target);
    }
}
