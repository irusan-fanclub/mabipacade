using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.World;

namespace Mabipacade.Decoders.Tests.World;

public class UseMotionDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x00006D62, new UseMotionDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        // raw 0x6D62: [Int 25, Int 21, Byte 0, Short 0, Short 0]
        var elems = new[]
        {
            MessageElem.Int(25),
            MessageElem.Int(21),
            MessageElem.Byte(0),
            MessageElem.Short(0),
            MessageElem.Short(0),
        };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x00006D62, 0UL, elems);
        var result = Assert.IsType<UseMotion>(new UseMotionDecoder().Decode(input));
        Assert.Equal((uint)25, result.Category);
        Assert.Equal((uint)21, result.Motion);
    }
}
