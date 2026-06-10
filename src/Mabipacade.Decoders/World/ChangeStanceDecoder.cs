using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.World;

public sealed record ChangeStance(byte Stance);

public sealed class ChangeStanceDecoder : IPacketDecoder
{
    public uint Op => 0x00006E2A;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        byte stance = e.Count > 0 && e[0].Type == MessageElemType.Byte ? e[0].AsByte() : (byte)0;
        return new ChangeStance(stance);
    }
}
