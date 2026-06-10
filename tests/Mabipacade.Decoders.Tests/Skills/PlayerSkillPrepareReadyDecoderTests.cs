using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Skills;

namespace Mabipacade.Decoders.Tests.Skills;

public class PlayerSkillPrepareReadyDecoderTests
{
    [Fact]
    public void DecodesSkillId_FromFirstElem()
    {
        var decoder = new PlayerSkillPrepareReadyDecoder();
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x6985, 12345UL,
            new[] { MessageElem.Short(59001) });
        var result = (PlayerSkillPrepareReady)decoder.Decode(input);
        Assert.Equal((ushort)59001, result.SkillId);
    }

    [Fact]
    public void Op_Is6985()
    {
        Assert.Equal((uint)0x6985, new PlayerSkillPrepareReadyDecoder().Op);
    }
}
