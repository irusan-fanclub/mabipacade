using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Prop;

namespace Mabipacade.Decoders.Tests.Prop;

public class PropAppearsDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x52D0, new PropAppearsDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        // PropInfo Bin: first 4 bytes = PropId 280 (0x00000118) LE, then RegionId 35004.
        var propInfo = new byte[]
        {
            0x18, 0x01, 0x00, 0x00, // PropId = 280
            0xBC, 0x88, 0x00, 0x00, // RegionId = 35004
            0x00, 0x00, 0x50, 0x00, // tail
        };

        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x52D0, 0UL,
            new[]
            {
                MessageElem.Long(45467812285972655UL),
                MessageElem.Int(280),
                MessageElem.String(""),
                MessageElem.String(""),
                MessageElem.Bin(propInfo),
                MessageElem.String("single"),
                MessageElem.Long(0),
                MessageElem.Byte(0),
                MessageElem.Int(0),
                MessageElem.Short(0),
            });

        var result = (PropAppears)new PropAppearsDecoder().Decode(input);

        Assert.Equal(280u, result.PropId);
        Assert.Equal(propInfo, result.PropInfo);
    }
}
