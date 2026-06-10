using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Skills;

namespace Mabipacade.Decoders.Tests.Skills;

public class PlayerSkillPostCastAck2DecoderTests
{
    [Fact]
    public void DecodesSkillId_FromFirstElem()
    {
        var decoder = new PlayerSkillPostCastAck2Decoder();
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x6989, 12345UL,
            new[] { MessageElem.Short(59003) });
        var result = (PlayerSkillPostCastAck2)decoder.Decode(input);
        Assert.Equal((ushort)59003, result.SkillId);
    }

    [Fact]
    public void Op_Is6989()
    {
        Assert.Equal((uint)0x6989, new PlayerSkillPostCastAck2Decoder().Op);
    }
}
