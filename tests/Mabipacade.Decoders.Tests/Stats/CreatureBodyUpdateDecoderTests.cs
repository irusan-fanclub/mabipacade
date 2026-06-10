using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Stats;

namespace Mabipacade.Decoders.Tests.Stats;

public class CreatureBodyUpdateDecoderTests
{
    [Fact]
    public void Op_Matches() => Assert.Equal((uint)0x520E, new CreatureBodyUpdateDecoder().Op);

    [Fact]
    public void Decodes_Sample()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x520E, 0UL,
            new List<MessageElem>
            {
                MessageElem.Long(123UL),
                MessageElem.Float(1.1f),
                MessageElem.Float(1.2f),
                MessageElem.Float(0.9f),
                MessageElem.Float(1.0f),
            });

        var r = (CreatureBodyUpdate)new CreatureBodyUpdateDecoder().Decode(input);

        Assert.Equal(123UL, r.EntityId);
        Assert.Equal(1.1f, r.Height);
        Assert.Equal(1.2f, r.Weight);
        Assert.Equal(0.9f, r.Upper);
        Assert.Equal(1.0f, r.Lower);
    }
}
