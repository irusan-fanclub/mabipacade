using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Pet;

namespace Mabipacade.Decoders.Tests.Pet;

public class PetCategoryDecodersTests
{
    [Fact]
    public void BuffState_Op_IsFull32Bit() => Assert.Equal(0x00021208u, new BuffStateUpdateDecoder().Op);

    [Fact]
    public void BuffState_Decodes_AddAndName()
    {
        var elems = new List<MessageElem>
        {
            MessageElem.Byte(2), MessageElem.Short(1), MessageElem.Byte(0),
            MessageElem.String("SZDA"),
        };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x00021208, 0UL, elems);
        var r = (BuffStateUpdate)new BuffStateUpdateDecoder().Decode(input);
        Assert.False(r.IsAdd);          // flag byte 0 = remove
        Assert.Equal("SZDA", r.Name);
    }

    [Fact]
    public void PetCapacity_Op_IsFull32Bit() => Assert.Equal(0x00020F6Fu, new PetCapacityDecoder().Op);

    [Fact]
    public void PetResourceSync_Decodes_PetId()
    {
        var elems = new List<MessageElem>
        {
            MessageElem.Int(0), MessageElem.Int(1),
            MessageElem.Long(4504699144053550), MessageElem.Int(32),
            MessageElem.Byte(0), MessageElem.Byte(0), MessageElem.Byte(0),
        };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x0001FBD4, 0UL, elems);
        var r = (PetResourceSync)new PetResourceSyncDecoder().Decode(input);
        Assert.Equal(4504699144053550UL, r.PetId);
        Assert.Equal(32u, r.Resource);
    }
}
