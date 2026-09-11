using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Pet;

namespace Mabipacade.Decoders.Tests.Pet;

public class PetKeepingTickDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x0000906F, new PetKeepingTickDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        // {Long slot/pet EID, Long FILETIME} - sample 08-10-36 L104.
        var elems = new[]
        {
            MessageElem.Long(4504699143875556UL),
            MessageElem.Long(63916589441916UL),
        };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x0000906F, 1UL, elems);
        var result = Assert.IsType<PetKeepingTick>(new PetKeepingTickDecoder().Decode(input));
        Assert.Equal(4504699143875556UL, result.SlotOrPetEid);
        Assert.Equal(63916589441916UL, result.Filetime);
    }
}
