using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Skills;

namespace Mabipacade.Decoders.Tests.Skills;

public class PlayerSkillStopDecoderTests
{
    [Fact]
    public void Decode_ReturnsPlayerSkillStop_RegardlessOfElems()
    {
        var decoder = new PlayerSkillStopDecoder();
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x698B, 12345UL,
            Array.Empty<MessageElem>());
        var result = decoder.Decode(input);
        Assert.IsType<PlayerSkillStop>(result);
    }

    [Fact]
    public void Op_Is698B()
    {
        Assert.Equal((ushort)0x698B, new PlayerSkillStopDecoder().Op);
    }
}
