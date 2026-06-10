using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Pet;

namespace Mabipacade.Decoders.Tests.Pet;

public class SummonPetRDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x0000902D, new SummonPetRDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        var elems = new[]
        {
            MessageElem.Byte(1),
            MessageElem.Long(4504699139850743UL),
        };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x0000902D, 4503599628180874UL, elems);
        var result = Assert.IsType<SummonPetR>(new SummonPetRDecoder().Decode(input));
        Assert.Equal((byte)1, result.Active);
        Assert.Equal(4504699139850743UL, result.PetId);
    }

    [Fact]
    public void Decodes_FailureWithoutPetId()
    {
        var elems = new[] { MessageElem.Byte(0) };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x0000902D, 4503599628180874UL, elems);
        var result = Assert.IsType<SummonPetR>(new SummonPetRDecoder().Decode(input));
        Assert.Equal((byte)0, result.Active);
        Assert.Equal(0UL, result.PetId);
    }
}
