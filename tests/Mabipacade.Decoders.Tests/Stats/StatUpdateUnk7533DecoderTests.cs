using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Stats;

namespace Mabipacade.Decoders.Tests.Stats;

public class StatUpdateUnk7533DecoderTests
{
    [Fact]
    public void Op_Matches() => Assert.Equal((uint)0x00007533, new StatUpdateUnk7533Decoder().Op);

    [Fact]
    public void Decodes_Sample()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x00007533, 0UL,
            new List<MessageElem> { MessageElem.Long(4767482419981534UL), MessageElem.Byte(1) });

        var r = (StatUpdateUnk7533)new StatUpdateUnk7533Decoder().Decode(input);

        Assert.Equal(4767482419981534UL, r.EntityId);
        Assert.Equal((byte)1, r.Flag);
    }
}
