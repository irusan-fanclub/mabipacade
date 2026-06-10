using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Pet;

namespace Mabipacade.Decoders.Tests.Pet;

public class TelePetRDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x9034, new TelePetRDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        var elems = new[]
        {
            MessageElem.Byte(1),
            MessageElem.Long(4504699144649501UL),
        };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x9034, 4503599628180874UL, elems);
        var result = Assert.IsType<TelePetR>(new TelePetRDecoder().Decode(input));
        Assert.Equal((byte)1, result.Success);
        Assert.Equal(4504699144649501UL, result.PetId);
    }
}
