using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Misc;

namespace Mabipacade.Decoders.Tests.Misc;

public class EquipmentChangedDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x59E6, new EquipmentChangedDecoder().Op);
    }

    [Fact]
    public void Decodes_ReturnsCorrectType()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x59E6, 0UL,
            Array.Empty<MessageElem>());
        Assert.IsType<EquipmentChanged>(new EquipmentChangedDecoder().Decode(input));
    }
}
