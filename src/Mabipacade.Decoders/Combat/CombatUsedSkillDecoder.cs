using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Combat;

public sealed record CombatUsedSkill(ushort SkillId);

public sealed class CombatUsedSkillDecoder : IPacketDecoder
{
    public uint Op => 0x00007927;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        ushort skill = e.Count > 0 && e[0].Type == MessageElemType.Short ? e[0].AsUInt16() : (ushort)0;
        return new CombatUsedSkill(skill);
    }
}
