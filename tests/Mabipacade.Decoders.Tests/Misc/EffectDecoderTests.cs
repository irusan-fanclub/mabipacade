using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Misc;

namespace Mabipacade.Decoders.Tests.Misc;

public class EffectDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x00009091, new EffectDecoder().Op);
    }

    [Fact]
    public void Decodes_ReturnsCorrectType()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x00009091, 0UL,
            Array.Empty<MessageElem>());
        Assert.IsType<Effect>(new EffectDecoder().Decode(input));
    }
}
