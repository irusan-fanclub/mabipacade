using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Pet;

namespace Mabipacade.Decoders.Tests.Pet;

public class SkillInfoDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x00006979, new SkillInfoDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        // 52 C3 -> 50002 LE, Rank 1; padded to 76 bytes.
        var bin = new byte[76];
        bin[0] = 0x52;
        bin[1] = 0xC3;
        bin[4] = 0x01;
        var elems = new[] { MessageElem.Bin(bin) };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x00006979, 4504699144649501UL, elems);
        var result = Assert.IsType<SkillInfo>(new SkillInfoDecoder().Decode(input));
        Assert.Equal((ushort)50002, result.SkillId);
        Assert.Equal(76, result.Info.Length);
    }
}
