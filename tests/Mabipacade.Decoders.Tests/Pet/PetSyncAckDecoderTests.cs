using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Pet;

namespace Mabipacade.Decoders.Tests.Pet;

public class PetSyncAckDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x0000AF10, new PetSyncAckDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x0000AF10, 1UL,
            Array.Empty<MessageElem>());
        var result = new PetSyncAckDecoder().Decode(input);
        Assert.NotNull(result);
        Assert.IsType<PetSyncAck>(result);
    }
}
