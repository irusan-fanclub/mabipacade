using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Entity;

namespace Mabipacade.Decoders.Tests.Entity;

public class EntityAppearDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x520C, new EntityAppearDecoder().Op);
    }

    [Fact]
    public void Decodes_ReturnsCorrectType()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x520C, 0UL,
            Array.Empty<MessageElem>());
        Assert.IsType<EntityAppear>(new EntityAppearDecoder().Decode(input));
    }

    [Fact]
    public void Decodes_ReturnsDefaultValues()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x520C, 0UL,
            Array.Empty<MessageElem>());
        var result = (EntityAppear)new EntityAppearDecoder().Decode(input);
        Assert.Equal(0u, result.RaceId);
        Assert.Equal("", result.Name);
    }
}
