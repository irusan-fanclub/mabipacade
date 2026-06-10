using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.World;

namespace Mabipacade.Decoders.Tests.World;

public class PointsUpdateDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x00004E90, new PointsUpdateDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        // raw 0x4E90: [Byte 2, Int 533]
        var elems = new[]
        {
            MessageElem.Byte(2),
            MessageElem.Int(533),
        };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x00004E90, 0UL, elems);
        var result = Assert.IsType<PointsUpdate>(new PointsUpdateDecoder().Decode(input));
        Assert.Equal((uint)533, result.Points);
    }
}
