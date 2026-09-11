using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Misc;

namespace Mabipacade.Decoders.Tests.Misc;

public class UnequipmentDecoderTests
{
    [Fact]
    public void Op_Matches() => Assert.Equal((uint)0x000059E7, new UnequipmentDecoder().Op);

    [Fact]
    public void Decodes_Sample()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x000059E7, 0UL,
            new List<MessageElem> { MessageElem.Int(5) });
        var r = (Unequipment)new UnequipmentDecoder().Decode(input);
        Assert.Equal(5u, r.Pocket);
    }
}
