using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.World;

namespace Mabipacade.Decoders.Tests.World;

public class EnterRegionRequestRDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x659C, new EnterRegionRequestRDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        // raw 0x659C: [Byte 1, Long 4503599630022047, Long 63916725322421]
        var elems = new[]
        {
            MessageElem.Byte(1),
            MessageElem.Long(4503599630022047UL),
            MessageElem.Long(63916725322421UL),
        };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x659C, 0UL, elems);
        var result = Assert.IsType<EnterRegionRequestR>(new EnterRegionRequestRDecoder().Decode(input));
        Assert.Equal((byte)1, result.Success);
        Assert.Equal(4503599630022047UL, result.EntityId);
        Assert.Equal(63916725322421UL, result.FileTime);
    }
}
