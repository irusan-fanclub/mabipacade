using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Entity;

namespace Mabipacade.Decoders.Tests.Entity;

public class EntitiesDisappearDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((ushort)0x5335, new EntitiesDisappearDecoder().Op);
    }

    [Fact]
    public void Decodes_ReturnsCorrectType()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x5335, 0UL,
            Array.Empty<MessageElem>());
        Assert.IsType<EntitiesDisappear>(new EntitiesDisappearDecoder().Decode(input));
    }
}
