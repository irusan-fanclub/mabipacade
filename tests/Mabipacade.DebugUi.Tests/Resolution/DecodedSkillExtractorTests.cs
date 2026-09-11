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
        var pack = new CombatActionPack(1u, 0UL, new[]
        {
            Attacker(12345),
            Attacker(50001),
        });
        var ids = DecodedSkillExtractor.ExtractSkillIds(pack);
        Assert.Contains(12345, ids);
        Assert.Contains(50001, ids);
    }

    [Fact]
    public void CombatActionPack_DoesNotReportVictimReactionAsUsedSkill()
    {
        // A victim's Defense (20001) is its answer to the attack, not a skill it used. The
        // field is deliberately not named "*SkillId" so reflection does not collect it.
        var pack = new CombatActionPack(1u, 0UL, new[]
        {
            Attacker(27203),
            new CombatSubAction(1u, 2UL, 1, false, 2000, 0, 20001, 0, 0, null, null),
        });
        var ids = DecodedSkillExtractor.ExtractSkillIds(pack);
        Assert.Contains(27203, ids);
        Assert.DoesNotContain(20001, ids);
    }

    private static CombatSubAction Attacker(ushort skillId) =>
        new(1u, 1UL, 2, true, 0, skillId, 0, 0, 0, null, null);
}
