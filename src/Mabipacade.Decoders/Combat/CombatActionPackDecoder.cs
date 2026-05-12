using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Combat;

public sealed record CombatSubAction(byte Type, ushort SkillId, ushort SubSkillId, ulong TargetId, int Damage);

public sealed record CombatActionPack(ulong AttackerId, IReadOnlyList<CombatSubAction> Sub);

public sealed class CombatActionPackDecoder : IPacketDecoder
{
    public ushort Op => 0x7926;
    public object Decode(DecoderInput input)
    {
        // Body shape is complex (sub-packets within Bin elems). M1 emits an
        // empty Sub list as a typed marker. Real decoding follows in v2 once
        // the reference parser is ported and verified against a real pcap.
        return new CombatActionPack(input.EntityId, Array.Empty<CombatSubAction>());
    }
}
