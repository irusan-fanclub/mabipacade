using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.World;

namespace Mabipacade.Decoders.Tests.World;

public class ChangeStanceDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x6E2A, new ChangeStanceDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        var elems = new[]
        {
            MessageElem.Byte(1),
            MessageElem.Byte(1),
            MessageElem.Byte(1),
        };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x6E2A, 4504699144649501UL, elems);
        var result = Assert.IsType<ChangeStance>(new ChangeStanceDecoder().Decode(input));
        Assert.Equal((byte)1, result.Stance);
    }
}
