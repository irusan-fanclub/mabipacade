using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Combat;

namespace Mabipacade.Decoders.Tests.Combat;

public class CombatUsedSkillDecoderTests
{
    [Fact]
    public void Op_Matches() => Assert.Equal((uint)0x7927, new CombatUsedSkillDecoder().Op);

    [Fact]
    public void Decodes_Sample()
    {
        var elems = new List<MessageElem> { MessageElem.Short(24101) };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x7927, 0UL, elems);
        var r = (CombatUsedSkill)new CombatUsedSkillDecoder().Decode(input);
        Assert.Equal((ushort)24101, r.SkillId);
    }
}
