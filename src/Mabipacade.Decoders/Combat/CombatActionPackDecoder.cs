using System.Buffers.Binary;
using Mabipacade.Core.Model;
using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Combat;

/// <summary>
/// One participant's record inside a <see cref="CombatActionPack"/>. On the wire this is a
/// complete 0x7924 packet (opcode + entity id + body) carried inside a Bin element; it has
/// never been observed standalone.
/// <para>
/// Each record is either the attacker's or one victim's, decided by the low bit of
/// <see cref="RawKind"/>. Exactly one attacker record and zero to eight victim records make
/// up a pack, and their order is not fixed — a victim record can precede the attacker's.
/// </para>
/// </summary>
/// <param name="PackId">Identifies the combat action; every record in one pack shares it.</param>
/// <param name="EntityId">Who this record is about — the attacker, or the victim it describes.</param>
/// <param name="RawKind">Raw kind byte. Observed in attacker/victim pairs 2/1, 18/17, 50/49, 66/65; what distinguishes the pairs is unknown.</param>
/// <param name="IsAttacker">True when <see cref="RawKind"/> is even.</param>
/// <param name="StunMs">Stun / knockdown duration in milliseconds.</param>
/// <param name="SkillId">The skill the attacker used. Zero on victim records — their skill field is <see cref="ReactionSkill"/>, which is a different thing.</param>
/// <param name="ReactionSkill">On a victim record, the skill the victim answered with (20001 Defense, 20002 Smash, 0 for none). Zero on attacker records. Deliberately not named "*SkillId": counting it as a used skill reports the defender's reaction as the attack.</param>
/// <param name="Unknown5">Element 5. Always 0 on attacker records; on victim records it takes values such as 35, 13102, 56907 that match neither an id nor a bitfield.</param>
/// <param name="Unknown6">Element 6. Small values (7, 9, 15, 16, 18); meaning unknown.</param>
/// <param name="Attacker">Set when <see cref="IsAttacker"/>.</param>
/// <param name="Victim">Set when this is a victim record.</param>
public sealed record CombatSubAction(
    uint PackId,
    ulong EntityId,
    byte RawKind,
    bool IsAttacker,
    ushort StunMs,
    ushort SkillId,
    ushort ReactionSkill,
    ushort Unknown5,
    ushort Unknown6,
    CombatAttackerInfo? Attacker,
    CombatVictimInfo? Victim);

/// <summary>A target expressed as a packed world point rather than an entity id.</summary>
/// <param name="Region">Region id, e.g. 35003.</param>
/// <param name="X">World X. The stored value is scaled by 20; the scale matches the attacker's own position on most records but has not been proven exact.</param>
/// <param name="Y">World Y, same scaling.</param>
/// <param name="Raw">The undecoded Long, kept so a consumer can re-derive the coordinates if the scale turns out to be wrong.</param>
public sealed record CombatTargetPoint(ushort Region, uint X, uint Y, ulong Raw);

/// <summary>
/// The attacker half of a combat action: what was used, where it was aimed, and where the
/// attacker stood.
/// </summary>
/// <param name="RawTarget">Element 7 verbatim. This field is a union — see <see cref="TargetEntityId"/> and <see cref="TargetPoint"/>.</param>
/// <param name="TargetEntityId">The entity or prop attacked, or 0 when none was resolved.</param>
/// <param name="TargetPoint">Set when the attack was aimed at a point instead of an entity; null otherwise.</param>
/// <param name="Flags">Element 8. Only bits 0x2, 0x4, 0x8, 0x20 and 0x400 have been observed.</param>
/// <param name="PosX">Where the attacker stood. Not necessarily the aim point.</param>
/// <param name="PosY">Where the attacker stood.</param>
public sealed record CombatAttackerInfo(
    ulong RawTarget,
    ulong TargetEntityId,
    CombatTargetPoint? TargetPoint,
    uint Flags,
    uint PosX,
    uint PosY);

/// <summary>
/// One victim's outcome: how much it took and how it was moved.
/// </summary>
/// <param name="Flags">Element 7, a bitfield. Bit 0x1000 accompanies damage roughly 2.9x the same skill's median and is most likely a critical, but that is inferred from medians, not from a controlled comparison.</param>
/// <param name="Damage">Damage dealt to this victim. This is the number a damage meter wants.</param>
/// <param name="Wound">Wound or follow-up damage. Zero on the large majority of records.</param>
/// <param name="KnockbackX">Knockback vector.</param>
/// <param name="KnockbackY">Knockback vector.</param>
/// <param name="PosX">Victim position — only present on the long form.</param>
/// <param name="PosY">Victim position — only present on the long form.</param>
/// <param name="DelayMs">Delay in milliseconds — only present on the long form.</param>
/// <param name="AttackerId">Who dealt this damage. Read from the record's own tail, so a victim record identifies its attacker without depending on the pack's attacker record.</param>
public sealed record CombatVictimInfo(
    uint Flags,
    float Damage,
    float Wound,
    float KnockbackX,
    float KnockbackY,
    float? PosX,
    float? PosY,
    uint? DelayMs,
    ulong AttackerId);

/// <summary>
/// 0x7926 CombatActionPack — the broadcast that carries the result of one combat action.
/// <para>
/// The packet describes no combat itself: it is a container whose tail is N pairs of
/// (Int length, Bin), each Bin a complete nested 0x7924 record. Everything about who hit
/// whom for how much lives in those records.
/// </para>
/// <para>
/// A damage meter needs only this opcode — the trailing 0x7925 carries the same pack id and
/// adds nothing. Deduplicate on <see cref="PackId"/>: captures have been seen to record every
/// packet twice, which silently doubles any total.
/// </para>
/// </summary>
/// <param name="PackId">Identifies this combat action.</param>
/// <param name="AttackerId">Taken from the attacker sub-record. The packet's own entity id is the broadcast constant 0x3000000000000000 and identifies nobody.</param>
/// <param name="Sub">The nested records, in wire order.</param>
public sealed record CombatActionPack(
    uint PackId,
    ulong AttackerId,
    IReadOnlyList<CombatSubAction> Sub);

public sealed class CombatActionPackDecoder : IPacketDecoder
{
    public uint Op => 0x00007926;

    /// <summary>Opcode every nested record starts with, used to reject Bin elements that are not sub-records.</summary>
    private const uint SubRecordOp = 0x00007924;

    /// <summary>Attacker flag: element 7 holds a packed point and the real target moves to element 14.</summary>
    private const uint AttackerFlagTargetIsPoint = 0x08;

    /// <summary>Any of these victim flag bits means the long, 24-element form.</summary>
    private const uint VictimFlagLongForm = 0x1F10;

    /// <summary>High 16 bits shared by the broadcast entity id and by packed target points.</summary>
    private const ulong PointPrefix = 0x3000;

    /// <summary>Packed coordinates are stored divided by this.</summary>
    private const uint CoordScale = 20;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        uint packId = e.Count > 0 && e[0].Type == MessageElemType.Int ? e[0].AsUInt32() : 0u;

        // Element 6 states the sub-record count, but the (length, Bin) pairs are taken by
        // walking for Bin elements rather than by offset: one capture-side variant inserts
        // three extra elements ahead of the count, and the Int lengths only restate each
        // Bin's own length. Sub-records self-identify by their leading opcode, so a Bin that
        // is not one cannot be mistaken for one.
        var sub = new List<CombatSubAction>();
        foreach (var el in e)
        {
            if (el.Type != MessageElemType.Bin) continue;
            var record = TryParseSubRecord(el.AsBytes());
            if (record is not null) sub.Add(record);
        }

        ulong attackerId = 0;
        foreach (var s in sub)
        {
            if (!s.IsAttacker) continue;
            attackerId = s.EntityId;
            break;
        }

        return new CombatActionPack(packId, attackerId, sub);
    }

    /// <summary>
    /// Parses one nested 0x7924 record. Returns null for a Bin that is not a sub-record or
    /// whose body does not parse — a malformed record is dropped rather than throwing, so one
    /// bad Bin cannot cost the whole pack.
    /// </summary>
    internal static CombatSubAction? TryParseSubRecord(byte[] bytes)
    {
        // opcode u32 BE + entity id u64 BE, then a standard message body.
        const int headerLen = 12;
        if (bytes.Length < headerLen) return null;
        if (BinaryPrimitives.ReadUInt32BigEndian(bytes) != SubRecordOp) return null;

        if (MessageElemReader.TryRead(bytes.AsSpan(headerLen), out var elems) != ReadElemsResult.Ok)
            return null;

        return ParseRecord(elems);
    }

    private static CombatSubAction? ParseRecord(IReadOnlyList<MessageElem> e)
    {
        // Prefix shared by both record kinds.
        if (e.Count < 7) return null;
        if (e[0].Type != MessageElemType.Int) return null;
        if (e[1].Type != MessageElemType.Long) return null;
        if (e[2].Type != MessageElemType.Byte) return null;
        for (int i = 3; i <= 6; i++)
            if (e[i].Type != MessageElemType.Short) return null;

        uint packId = e[0].AsUInt32();
        ulong entityId = e[1].AsUInt64();
        byte rawKind = e[2].AsByte();
        ushort stunMs = e[3].AsUInt16();
        ushort skillField = e[4].AsUInt16();
        ushort unknown5 = e[5].AsUInt16();
        ushort unknown6 = e[6].AsUInt16();

        // The low bit of the kind byte is the discriminator: even is the attacker's record,
        // odd is a victim's. The observed pairs (2/1, 18/17, 66/65) differ by exactly one.
        bool isAttacker = (rawKind & 1) == 0;

        CombatAttackerInfo? attacker = isAttacker ? ParseAttacker(e) : null;
        CombatVictimInfo? victim = isAttacker ? null : ParseVictim(e);
        if (isAttacker ? attacker is null : victim is null) return null;

        return new CombatSubAction(
            packId,
            entityId,
            rawKind,
            isAttacker,
            stunMs,
            isAttacker ? skillField : (ushort)0,
            isAttacker ? (ushort)0 : skillField,
            unknown5,
            unknown6,
            attacker,
            victim);
    }

    private static CombatAttackerInfo? ParseAttacker(IReadOnlyList<MessageElem> e)
    {
        // 7: Long target, 8: Int flags, 9: Byte, 10: Byte, 11: Int, 12: Int x, 13: Int y,
        // 14: Long target entity, present only when the point flag is set.
        if (e.Count < 14) return null;
        if (e[7].Type != MessageElemType.Long) return null;
        if (e[8].Type != MessageElemType.Int) return null;
        if (e[12].Type != MessageElemType.Int || e[13].Type != MessageElemType.Int) return null;

        ulong rawTarget = e[7].AsUInt64();
        uint flags = e[8].AsUInt32();
        uint posX = e[12].AsUInt32();
        uint posY = e[13].AsUInt32();

        // Element 7 is a union. With the point flag set it holds an aim point and the entity
        // actually struck moves to element 14; without it, element 7 is the entity id itself
        // and element 14 is absent. Reading it as an entity id unconditionally yields the
        // 0x3000-prefixed value, which is not an entity that exists.
        bool targetIsPoint = (flags & AttackerFlagTargetIsPoint) != 0;
        CombatTargetPoint? point = null;
        ulong targetEntityId;

        if (targetIsPoint)
        {
            point = TryDecodePoint(rawTarget);
            targetEntityId = e.Count >= 15 && e[14].Type == MessageElemType.Long
                ? e[14].AsUInt64()
                : 0UL;
        }
        else
        {
            targetEntityId = rawTarget;
        }

        return new CombatAttackerInfo(rawTarget, targetEntityId, point, flags, posX, posY);
    }

    private static CombatVictimInfo? ParseVictim(IReadOnlyList<MessageElem> e)
    {
        // 7: Int flags, 8: Float damage, 9: Float wound, 10-12: Int zeros,
        // 13/14: Float knockback, then optionally 15/16: Float position and 17: Int delay,
        // then a six-element tail.
        if (e.Count < 15) return null;
        if (e[7].Type != MessageElemType.Int) return null;
        if (e[8].Type != MessageElemType.Float || e[9].Type != MessageElemType.Float) return null;
        if (e[13].Type != MessageElemType.Float || e[14].Type != MessageElemType.Float) return null;

        uint flags = e[7].AsUInt32();
        float damage = e[8].AsFloat();
        float wound = e[9].AsFloat();
        float knockX = e[13].AsFloat();
        float knockY = e[14].AsFloat();

        // The optional block is inserted in the middle, not appended, so its presence shifts
        // everything after it. The flags decide it; the length check is a second opinion, so a
        // disagreement degrades to "no optional block" instead of misreading the tail.
        float? posX = null, posY = null;
        uint? delayMs = null;
        if ((flags & VictimFlagLongForm) != 0
            && e.Count >= 24
            && e[15].Type == MessageElemType.Float
            && e[16].Type == MessageElemType.Float
            && e[17].Type == MessageElemType.Int)
        {
            posX = e[15].AsFloat();
            posY = e[16].AsFloat();
            delayMs = e[17].AsUInt32();
        }

        // The tail is six elements and is read backwards for that reason: the fourth from the
        // end is the attacker. Indexing it from the front picks up a different field whenever
        // the optional block is present.
        ulong attackerId = 0;
        int attackerIndex = e.Count - 4;
        if (attackerIndex >= 15 && e[attackerIndex].Type == MessageElemType.Long)
            attackerId = e[attackerIndex].AsUInt64();

        return new CombatVictimInfo(flags, damage, wound, knockX, knockY, posX, posY, delayMs, attackerId);
    }

    /// <summary>
    /// Unpacks the point form of a target Long: a 0x3000 marker, then region, X and Y in
    /// 16-bit fields, the coordinates divided by <see cref="CoordScale"/>.
    /// </summary>
    internal static CombatTargetPoint? TryDecodePoint(ulong raw)
    {
        if ((raw >> 48) != PointPrefix) return null;
        ushort region = (ushort)((raw >> 32) & 0xFFFF);
        uint x = (uint)((raw >> 16) & 0xFFFF) * CoordScale;
        uint y = (uint)(raw & 0xFFFF) * CoordScale;
        return new CombatTargetPoint(region, x, y, raw);
    }
}
