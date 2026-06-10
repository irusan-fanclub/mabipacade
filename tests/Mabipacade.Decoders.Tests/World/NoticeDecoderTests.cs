using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.World;

namespace Mabipacade.Decoders.Tests.World;

public class NoticeDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x526D, new NoticeDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        var elems = new[]
        {
            MessageElem.Byte(14),
            MessageElem.String("[CHANNEL13]Snow Giant"),
            MessageElem.Int(20000),
            MessageElem.Int(1507),
        };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x526D, 3458764513820540928UL, elems);
        var result = Assert.IsType<Notice>(new NoticeDecoder().Decode(input));
        Assert.Equal("[CHANNEL13]Snow Giant", result.Message);
        Assert.Equal((uint)20000, result.Duration);
    }
}
