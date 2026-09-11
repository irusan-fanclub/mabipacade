using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Misc;

namespace Mabipacade.Decoders.Tests.Misc;

public class PartyWindowUpdateDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x0000A43C, new PartyWindowUpdateDecoder().Op);
    }

    [Fact]
    public void Decodes_EntityIdFromHeader_AndBodyValues()
    {
        var elems = new List<MessageElem> { MessageElem.Short(0), MessageElem.Int(0) };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x0000A43C,
            4504699143795610UL, elems);
        var r = (PartyWindowUpdate)new PartyWindowUpdateDecoder().Decode(input);
        Assert.Equal(4504699143795610UL, r.EntityId);
        Assert.Equal((ushort)0, r.Value1);
        Assert.Equal(0u, r.Value2);
    }
}
