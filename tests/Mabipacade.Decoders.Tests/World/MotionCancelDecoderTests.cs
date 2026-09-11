using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.World;

namespace Mabipacade.Decoders.Tests.World;

public class MotionCancelDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x00006D66, new MotionCancelDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        var elems = new[] { MessageElem.Byte(0) };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x00006D66, 0UL, elems);
        var result = Assert.IsType<MotionCancel>(new MotionCancelDecoder().Decode(input));
        Assert.Equal((byte)0, result.Value);
    }
}
