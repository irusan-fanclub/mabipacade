using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Entity;

namespace Mabipacade.Decoders.Tests.Entity;

public class IsNowDeadDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((ushort)0x53FC, new IsNowDeadDecoder().Op);
    }

    [Fact]
    public void Decodes_ReturnsCorrectType()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x53FC, 0UL,
            Array.Empty<MessageElem>());
        Assert.IsType<IsNowDead>(new IsNowDeadDecoder().Decode(input));
    }
}
