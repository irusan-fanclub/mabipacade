using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.World;

public sealed record MotionCancel(byte Value);

public sealed class MotionCancelDecoder : IPacketDecoder
{
    public uint Op => 0x00006D66;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        byte value = e.Count > 0 && e[0].Type == MessageElemType.Byte ? e[0].AsByte() : (byte)0;
        return new MotionCancel(value);
    }
}
