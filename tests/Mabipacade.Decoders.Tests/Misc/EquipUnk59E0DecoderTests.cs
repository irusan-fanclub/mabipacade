using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Misc;

namespace Mabipacade.Decoders.Tests.Misc;

public class EquipUnk59E0DecoderTests
{
    [Fact]
    public void Op_Matches() => Assert.Equal((uint)0x000059E0, new EquipUnk59E0Decoder().Op);

    [Fact]
    public void Decodes_Sample()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x000059E0, 0UL,
            new List<MessageElem>
            {
                MessageElem.Long(22518903766512199UL),
                MessageElem.Byte(2),
                MessageElem.Bin(new byte[] { 0x17, 0x00 }),
            });

        var r = (EquipUnk59E0)new EquipUnk59E0Decoder().Decode(input);

        Assert.Equal(22518903766512199UL, r.InstanceId);
        Assert.Equal(3, r.ElemCount);
    }
}
