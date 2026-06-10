using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Stats;

namespace Mabipacade.Decoders.Tests.Stats;

public class StatUpdatePrivateDecoderTests
{
    [Fact]
    public void Op_Matches() => Assert.Equal((uint)0x7530, new StatUpdatePrivateDecoder().Op);

    [Fact]
    public void Decodes_MixedTypeStatPairs()
    {
        // count=3: (Int 28 -> Float 7370.07), (Int 32 -> Float 5392), (Int 94 -> Int 100)
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x7530, 0UL,
            new List<MessageElem>
            {
                MessageElem.Byte(3),
                MessageElem.Int(28), MessageElem.Float(7370.07f),
                MessageElem.Int(32), MessageElem.Float(5392f),
                MessageElem.Int(94), MessageElem.Int(100),
            });

        var r = (StatUpdatePrivate)new StatUpdatePrivateDecoder().Decode(input);

        Assert.Equal(3, r.Stats.Count);
        Assert.Equal(28u, r.Stats[0].StatId);
        Assert.Equal(7370.07, r.Stats[0].Value, 2);
        Assert.Equal(32u, r.Stats[1].StatId);
        Assert.Equal(5392.0, r.Stats[1].Value, 3);
        Assert.Equal(94u, r.Stats[2].StatId);
        Assert.Equal(100.0, r.Stats[2].Value, 3);
    }

    [Fact]
    public void Decodes_StopsOnMismatch()
    {
        // count claims 3 but only one valid pair before a String value breaks it.
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x7530, 0UL,
            new List<MessageElem>
            {
                MessageElem.Byte(3),
                MessageElem.Int(10), MessageElem.Short(5),
                MessageElem.Int(11), MessageElem.String("oops"),
            });

        var r = (StatUpdatePrivate)new StatUpdatePrivateDecoder().Decode(input);

        Assert.Single(r.Stats);
        Assert.Equal(10u, r.Stats[0].StatId);
        Assert.Equal(5.0, r.Stats[0].Value, 3);
    }

    [Fact]
    public void Decodes_EmptyWhenNoCount()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x7530, 0UL,
            Array.Empty<MessageElem>());
        var r = (StatUpdatePrivate)new StatUpdatePrivateDecoder().Decode(input);
        Assert.Empty(r.Stats);
    }
}
