using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.World;

namespace Mabipacade.Decoders.Tests.World;

public class DisappearDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x4E2A, new DisappearDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        // raw 0x4E2A: [Long 4504699144673104]
        var elems = new[] { MessageElem.Long(4504699144673104UL) };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x4E2A, 0UL, elems);
        var result = Assert.IsType<Disappear>(new DisappearDecoder().Decode(input));
        Assert.Equal(4504699144673104UL, result.EntityId);
    }
}
