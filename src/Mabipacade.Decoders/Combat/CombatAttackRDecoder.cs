using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Combat;

public sealed record CombatAttackR(byte Result);

public sealed class CombatAttackRDecoder : IPacketDecoder
{
    public uint Op => 0x7D01;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        byte result = e.Count > 0 && e[0].Type == MessageElemType.Byte ? e[0].AsByte() : (byte)0;
        return new CombatAttackR(result);
    }
}
