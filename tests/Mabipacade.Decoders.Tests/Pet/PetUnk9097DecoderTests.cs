using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Pet;

namespace Mabipacade.Decoders.Tests.Pet;

public class PetUnk9097DecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x9097, new PetUnk9097Decoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        var elems = new[] { MessageElem.Long(4503599628180874UL) };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x9097, 1UL, elems);
        var result = Assert.IsType<PetUnk9097>(new PetUnk9097Decoder().Decode(input));
        Assert.Equal(4503599628180874UL, result.Value);
    }
}
