using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Entity;

namespace Mabipacade.Decoders.Tests.Entity;

public class EntityDisappearDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((ushort)0x520D, new EntityDisappearDecoder().Op);
    }

    [Fact]
    public void Decodes_ReturnsCorrectType()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x520D, 0UL,
            Array.Empty<MessageElem>());
        Assert.IsType<EntityDisappear>(new EntityDisappearDecoder().Decode(input));
    }
}
