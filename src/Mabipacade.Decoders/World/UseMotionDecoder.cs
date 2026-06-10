using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.World;

public sealed record UseMotion(uint Category, uint Motion);

public sealed class UseMotionDecoder : IPacketDecoder
{
    public uint Op => 0x00006D62;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        uint category = e.Count > 0 && e[0].Type == MessageElemType.Int ? e[0].AsUInt32() : 0;
        uint motion = e.Count > 1 && e[1].Type == MessageElemType.Int ? e[1].AsUInt32() : 0;
        return new UseMotion(category, motion);
    }
}
