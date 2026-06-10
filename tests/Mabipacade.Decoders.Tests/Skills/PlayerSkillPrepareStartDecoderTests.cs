using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Skills;

namespace Mabipacade.Decoders.Tests.Skills;

public class PlayerSkillPrepareStartDecoderTests
{
    [Fact]
    public void DecodesSkillId_FromFirstElem()
    {
        var decoder = new PlayerSkillPrepareStartDecoder();
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x6984, 12345UL,
            new[] { MessageElem.Short(59000) });
        var result = (PlayerSkillPrepareStart)decoder.Decode(input);
        Assert.Equal((ushort)59000, result.SkillId);
    }

    [Fact]
    public void Op_Is6984()
    {
        Assert.Equal((uint)0x6984, new PlayerSkillPrepareStartDecoder().Op);
    }
}
