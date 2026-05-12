using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Skills;

namespace Mabipacade.Decoders.Tests.Skills;

public class PlayerSkillPrepareProgressDecoderTests
{
    [Fact]
    public void DecodesSkillId_FromThirdElem()
    {
        var decoder = new PlayerSkillPrepareProgressDecoder();
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x6993, 12345UL,
            new[] { MessageElem.Short(0), MessageElem.Short(0), MessageElem.Short(59004) });
        var result = (PlayerSkillPrepareProgress)decoder.Decode(input);
        Assert.Equal((ushort)59004, result.SkillId);
    }

    [Fact]
    public void Op_Is6993()
    {
        Assert.Equal((ushort)0x6993, new PlayerSkillPrepareProgressDecoder().Op);
    }
}
