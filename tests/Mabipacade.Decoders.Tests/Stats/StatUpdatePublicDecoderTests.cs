using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Stats;

namespace Mabipacade.Decoders.Tests.Stats;

public class StatUpdatePublicDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x7532, new StatUpdatePublicDecoder().Op);
    }

    [Fact]
    public void Decodes_ReturnsCorrectType()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x7532, 0UL,
            Array.Empty<MessageElem>());
        Assert.IsType<StatUpdatePublic>(new StatUpdatePublicDecoder().Decode(input));
    }
}
