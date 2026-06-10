using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Pet;

public sealed record PetSkillList(IReadOnlyList<(ushort SkillId, byte Rank)> Skills);

public sealed class PetSkillListDecoder : IPacketDecoder
{
    public uint Op => 0x69A4;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        var skills = new List<(ushort SkillId, byte Rank)>();
        // Walk elems in (Short skillId, Byte rank) pairs until they run out or types mismatch.
        for (int i = 0; i + 1 < e.Count; i += 2)
        {
            if (e[i].Type != MessageElemType.Short || e[i + 1].Type != MessageElemType.Byte)
                break;
            skills.Add((e[i].AsUInt16(), e[i + 1].AsByte()));
        }
        return new PetSkillList(skills);
    }
}
