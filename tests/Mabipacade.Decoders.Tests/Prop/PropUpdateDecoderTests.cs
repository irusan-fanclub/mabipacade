using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Prop;

namespace Mabipacade.Decoders.Tests.Prop;

public class PropUpdateDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x000052D2, new PropUpdateDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        // { String state, Long ts(0), Byte hasXml, Float direction, Short 0 }
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x000052D2, 45467812285972655UL,
            new[]
            {
                MessageElem.String("single"),
                MessageElem.Long(0),
                MessageElem.Byte(0),
                MessageElem.Float(5.23f),
                MessageElem.Short(0),
            });

        var result = (PropUpdate)new PropUpdateDecoder().Decode(input);

        Assert.Equal("single", result.State);
        Assert.Equal(5.23f, result.Direction);
    }
}
