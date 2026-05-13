using Mabipacade.DebugUi.Resolution;
using Mabipacade.Decoders.Combat;
using Mabipacade.Decoders.Skills;

namespace Mabipacade.DebugUi.Tests.Resolution;

public class DecodedSkillExtractorTests
{
    [Fact]
    public void Null_ReturnsEmpty()
    {
        Assert.Empty(DecodedSkillExtractor.ExtractSkillIds(null));
    }

    [Fact]
    public void PlayerSkillPrepareStart_ExtractsSkillId()
    {
        var decoded = new PlayerSkillPrepareStart((ushort)59000);
        var ids = DecodedSkillExtractor.ExtractSkillIds(decoded);
        Assert.Contains(59000, ids);
    }

    [Fact]
    public void CombatActionPack_ExtractsSubActionSkillIds()
    {
        var pack = new CombatActionPack(1UL, new[]
        {
            new CombatSubAction(0, (ushort)12345, (ushort)0, 0UL, 0),
            new CombatSubAction(0, (ushort)50001, (ushort)11111, 0UL, 0),
        });
        var ids = DecodedSkillExtractor.ExtractSkillIds(pack);
        Assert.Contains(12345, ids);
        Assert.Contains(50001, ids);
        Assert.Contains(11111, ids);
    }
}
