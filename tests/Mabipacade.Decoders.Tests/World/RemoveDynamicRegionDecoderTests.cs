using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.World;

namespace Mabipacade.Decoders.Tests.World;

public class RemoveDynamicRegionDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x00009572, new RemoveDynamicRegionDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        var elems = new[] { MessageElem.Int(35004) };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x00009572, 3458764513820540928UL, elems);
        var result = Assert.IsType<RemoveDynamicRegion>(new RemoveDynamicRegionDecoder().Decode(input));
        Assert.Equal((uint)35004, result.RegionId);
    }
}
