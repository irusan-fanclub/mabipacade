using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Pet;

namespace Mabipacade.Decoders.Tests.Pet;

public class PetFarewellDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0xAD04, new PetFarewellDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0xAD04, 1UL,
            Array.Empty<MessageElem>());
        var result = new PetFarewellDecoder().Decode(input);
        Assert.NotNull(result);
        Assert.IsType<PetFarewell>(result);
    }
}
