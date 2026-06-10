using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Prop;

namespace Mabipacade.Decoders.Tests.Prop;

public class PropDisappearsDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x52D1, new PropDisappearsDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x52D1, 0UL,
            new[] { MessageElem.Long(45467812285972655UL) });

        var result = (PropDisappears)new PropDisappearsDecoder().Decode(input);

        Assert.Equal(45467812285972655UL, result.PropEid);
    }
}
