using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Combat;

public sealed record CombatSetAimR(byte Flag);

public sealed class CombatSetAimRDecoder : IPacketDecoder
{
    public uint Op => 0x791E;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        byte flag = e.Count > 0 && e[0].Type == MessageElemType.Byte ? e[0].AsByte() : (byte)0;
        return new CombatSetAimR(flag);
    }
}
