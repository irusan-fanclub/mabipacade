using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Stats;

namespace Mabipacade.Decoders.Tests.Stats;

public class StatUpdatePublicDecoderTests
{
    [Fact]
    public void Op_Matches() => Assert.Equal((uint)0x7532, new StatUpdatePublicDecoder().Op);

    [Fact]
    public void Decodes_MixedTypeStatPairs()
    {
        // count=2: (Int 5 -> Byte 7), (Int 9 -> Long 123456789)
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x7532, 0UL,
            new List<MessageElem>
            {
                MessageElem.Byte(2),
                MessageElem.Int(5), MessageElem.Byte(7),
                MessageElem.Int(9), MessageElem.Long(123456789UL),
            });

        var r = (StatUpdatePublic)new StatUpdatePublicDecoder().Decode(input);

        Assert.Equal(2, r.Stats.Count);
        Assert.Equal((5u, 7.0), r.Stats[0]);
        Assert.Equal(9u, r.Stats[1].StatId);
        Assert.Equal(123456789.0, r.Stats[1].Value, 0);
    }
}
