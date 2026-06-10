using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Pet;

namespace Mabipacade.Decoders.Tests.Pet;

public class PetRegisterDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x9024, new PetRegisterDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        var elems = new[]
        {
            MessageElem.Long(4504699139850743UL),
            MessageElem.Byte(2),
        };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x9024, 4503599628180874UL, elems);
        var result = Assert.IsType<PetRegister>(new PetRegisterDecoder().Decode(input));
        Assert.Equal(4504699139850743UL, result.PetId);
        Assert.Equal((byte)2, result.Flag);
    }
}
