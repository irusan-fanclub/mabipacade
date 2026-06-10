using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Skills;

namespace Mabipacade.Decoders.Tests.Skills;

public class PlayerSkillPostCastAck1DecoderTests
{
    [Fact]
    public void DecodesSkillId_FromFirstElem()
    {
        var decoder = new PlayerSkillPostCastAck1Decoder();
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x00006988, 12345UL,
            new[] { MessageElem.Short(59002) });
        var result = (PlayerSkillPostCastAck1)decoder.Decode(input);
        Assert.Equal((ushort)59002, result.SkillId);
    }

    [Fact]
    public void Op_Is6988()
    {
        Assert.Equal((uint)0x00006988, new PlayerSkillPostCastAck1Decoder().Op);
    }
}
