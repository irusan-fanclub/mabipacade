using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Misc;

namespace Mabipacade.Decoders.Tests.Misc;

public class ItemUpdateDecoderTests
{
    [Fact]
    public void Op_Matches() => Assert.Equal((uint)0x59EE, new ItemUpdateDecoder().Op);

    [Fact]
    public void Decodes_Sample()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x59EE, 0UL,
            new List<MessageElem>
            {
                MessageElem.Long(22518903747423821UL),
                MessageElem.Short(88),
                MessageElem.Int(126),
            });

        var r = (ItemUpdate)new ItemUpdateDecoder().Decode(input);

        Assert.Equal(22518903747423821UL, r.InstanceId);
        Assert.Equal((ushort)88, r.Short);
        Assert.Equal(126u, r.Int);
    }
}
