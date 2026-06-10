using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Pet;

namespace Mabipacade.Decoders.Tests.Pet;

public class PetPrpDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x0000AE0F, new PetPrpDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        // {Byte 3, Int 615, Int 0, Int 0, Int 0} - sample 08-10-36 (搞雞寶).
        var elems = new[]
        {
            MessageElem.Byte(3),
            MessageElem.Int(615),
            MessageElem.Int(0),
            MessageElem.Int(0),
            MessageElem.Int(0),
        };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x0000AE0F, 1UL, elems);
        var result = Assert.IsType<PetPrp>(new PetPrpDecoder().Decode(input));
        Assert.Equal(615u, result.Prp);
        Assert.Equal(0u, result.Stage);
        Assert.Equal(0u, result.C);
        Assert.Equal(0u, result.D);
    }
}
