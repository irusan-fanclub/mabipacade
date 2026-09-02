using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Entity;

namespace Mabipacade.Decoders.Tests.Entity;

public class EntityDisappearDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x0000520D, new EntityDisappearDecoder().Op);
    }

    [Fact]
    public void Decodes_EntityIdAndValue1()
    {
        // Captured 2026-08-19: elems = { Long eid, Byte 1 }.
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x0000520D, 0UL,
            new[] { MessageElem.Long(0x0010F000001CA88EUL), MessageElem.Byte(1) });
        var decoded = Assert.IsType<EntityDisappear>(new EntityDisappearDecoder().Decode(input));
        Assert.Equal(0x0010F000001CA88EUL, decoded.EntityId);
        Assert.Equal((byte)1, decoded.Value1);
    }

    [Fact]
    public void Decodes_EmptyBody_DefaultsToZero()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x0000520D, 0UL,
            Array.Empty<MessageElem>());
        var decoded = Assert.IsType<EntityDisappear>(new EntityDisappearDecoder().Decode(input));
        Assert.Equal(0UL, decoded.EntityId);
        Assert.Equal((byte)0, decoded.Value1);
    }
}
