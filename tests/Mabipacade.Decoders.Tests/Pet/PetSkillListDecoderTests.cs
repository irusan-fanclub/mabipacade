using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Pet;

namespace Mabipacade.Decoders.Tests.Pet;

public class PetSkillListDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x69A4, new PetSkillListDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        var elems = new[]
        {
            MessageElem.Short(10004), MessageElem.Byte(1),
            MessageElem.Short(20002), MessageElem.Byte(1),
            MessageElem.Short(50002), MessageElem.Byte(1),
        };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x69A4, 4504699139850743UL, elems);
        var result = Assert.IsType<PetSkillList>(new PetSkillListDecoder().Decode(input));
        Assert.Equal(3, result.Skills.Count);
        Assert.Equal(((ushort)10004, (byte)1), result.Skills[0]);
        Assert.Equal(((ushort)20002, (byte)1), result.Skills[1]);
        Assert.Equal(((ushort)50002, (byte)1), result.Skills[2]);
    }
}
