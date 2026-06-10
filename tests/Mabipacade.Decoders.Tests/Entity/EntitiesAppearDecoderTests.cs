using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Entity;

namespace Mabipacade.Decoders.Tests.Entity;

public class EntitiesAppearDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x00005334, new EntitiesAppearDecoder().Op);
    }

    [Fact]
    public void Decodes_ReturnsCorrectType()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x00005334, 0UL,
            Array.Empty<MessageElem>());
        Assert.IsType<EntitiesAppear>(new EntitiesAppearDecoder().Decode(input));
    }
}
