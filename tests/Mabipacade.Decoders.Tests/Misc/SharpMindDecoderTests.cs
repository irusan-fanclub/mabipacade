using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Misc;

namespace Mabipacade.Decoders.Tests.Misc;

public class SharpMindDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((ushort)0xA41E, new SharpMindDecoder().Op);
    }

    [Fact]
    public void Decodes_ReturnsCorrectType()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0xA41E, 0UL,
            Array.Empty<MessageElem>());
        Assert.IsType<SharpMind>(new SharpMindDecoder().Decode(input));
    }
}
