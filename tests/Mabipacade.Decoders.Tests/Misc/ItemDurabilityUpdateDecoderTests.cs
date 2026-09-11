using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Misc;

namespace Mabipacade.Decoders.Tests.Misc;

public class ItemDurabilityUpdateDecoderTests
{
    [Fact]
    public void Op_Matches() => Assert.Equal((uint)0x00005BD5, new ItemDurabilityUpdateDecoder().Op);

    [Fact]
    public void Decodes_Sample()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x00005BD5, 0UL,
            new List<MessageElem> { MessageElem.Long(22518903381628624UL), MessageElem.Int(14566) });

        var r = (ItemDurabilityUpdate)new ItemDurabilityUpdateDecoder().Decode(input);

        Assert.Equal(22518903381628624UL, r.InstanceId);
        Assert.Equal(14566u, r.Durability);
    }
}
