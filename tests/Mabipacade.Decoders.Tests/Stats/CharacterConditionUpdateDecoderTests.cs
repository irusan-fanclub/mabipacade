using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Stats;

namespace Mabipacade.Decoders.Tests.Stats;

public class CharacterConditionUpdateDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x0000A028, new CharacterConditionUpdateDecoder().Op);
    }

    [Fact]
    public void Decodes_ReturnsCorrectType()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x0000A028, 0UL,
            Array.Empty<MessageElem>());
        Assert.IsType<CharacterConditionUpdate>(new CharacterConditionUpdateDecoder().Decode(input));
    }
}
