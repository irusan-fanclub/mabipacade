using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Ui;

namespace Mabipacade.Decoders.Tests.Ui;

public class RegionStatDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x0000A93B, new RegionStatDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x0000A93B, 0UL,
            new[]
            {
                MessageElem.Int(35004),
                MessageElem.Int(0),
            });

        var result = (RegionStat)new RegionStatDecoder().Decode(input);

        Assert.Equal(35004u, result.RegionId);
        Assert.Equal(0u, result.Value);
    }
}
