using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Pet;

namespace Mabipacade.Decoders.Tests.Pet;

public class PetUnregisterDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x00009025, new PetUnregisterDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        var elems = new[] { MessageElem.Long(4504699144649501UL) };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x00009025, 4503599628180874UL, elems);
        var result = Assert.IsType<PetUnregister>(new PetUnregisterDecoder().Decode(input));
        Assert.Equal(4504699144649501UL, result.PetId);
    }
}
