using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Stats;

namespace Mabipacade.Decoders.Tests.Stats;

public class StatUpdatePrivateDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((ushort)0x7530, new StatUpdatePrivateDecoder().Op);
    }

    [Fact]
    public void Decodes_ReturnsCorrectType()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x7530, 0UL,
            Array.Empty<MessageElem>());
        Assert.IsType<StatUpdatePrivate>(new StatUpdatePrivateDecoder().Decode(input));
    }
}
