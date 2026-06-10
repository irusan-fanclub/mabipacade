using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Pet;

namespace Mabipacade.Decoders.Tests.Pet;

public class PetEquipSlotInitDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x90A7, new PetEquipSlotInitDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        var elems = new[]
        {
            MessageElem.Byte(6),
            MessageElem.Byte(1),
            MessageElem.Int(0),
            MessageElem.Int(0),
        };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x90A7, 1UL, elems);
        var result = Assert.IsType<PetEquipSlotInit>(new PetEquipSlotInitDecoder().Decode(input));
        Assert.Equal((byte)6, result.Slot);
        Assert.Equal((byte)1, result.Flag);
        Assert.Equal(0u, result.A);
        Assert.Equal(0u, result.B);
    }
}
