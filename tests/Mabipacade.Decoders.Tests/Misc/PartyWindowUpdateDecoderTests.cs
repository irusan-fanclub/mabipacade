using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Misc;

namespace Mabipacade.Decoders.Tests.Misc;

public class PartyWindowUpdateDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0xA43C, new PartyWindowUpdateDecoder().Op);
    }

    [Fact]
    public void Decodes_ReturnsCorrectType()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0xA43C, 0UL,
            Array.Empty<MessageElem>());
        Assert.IsType<PartyWindowUpdate>(new PartyWindowUpdateDecoder().Decode(input));
    }
}
