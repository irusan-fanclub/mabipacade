using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Pet;

namespace Mabipacade.Decoders.Tests.Pet;

public class PetSummonAckDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x00009070, new PetSummonAckDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x00009070, 1UL,
            Array.Empty<MessageElem>());
        var result = new PetSummonAckDecoder().Decode(input);
        Assert.NotNull(result);
        Assert.IsType<PetSummonAck>(result);
    }
}
