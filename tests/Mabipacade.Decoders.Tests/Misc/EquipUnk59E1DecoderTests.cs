using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Misc;

namespace Mabipacade.Decoders.Tests.Misc;

public class EquipUnk59E1DecoderTests
{
    [Fact]
    public void Op_Matches() => Assert.Equal((uint)0x000059E1, new EquipUnk59E1Decoder().Op);

    [Fact]
    public void Decodes_Sample()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x000059E1, 0UL,
            new List<MessageElem> { MessageElem.Long(22518902530638255UL), MessageElem.Int(12) });

        var r = (EquipUnk59E1)new EquipUnk59E1Decoder().Decode(input);

        Assert.Equal(22518902530638255UL, r.InstanceId);
        Assert.Equal(12u, r.Value);
    }
}
