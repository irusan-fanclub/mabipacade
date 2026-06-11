namespace Mabipacade.Decoders.Snapshot;

/// <summary>A natural-recovery entry from the snapshot's regen list.</summary>
public sealed record SnapshotRegen(uint Id, float Change, int TimeLeft, uint Stat, float Max);

/// <summary>A bag item parsed from the inventory section's 80-byte item core.</summary>
public sealed record SnapshotItem(
    uint RecType,        // 2 = bag cell, 20/86/100/101 = pet-carried / sub-bag
    uint ItemId,
    uint Quantity,
    uint PosX,           // 0-based
    uint PosY,
    ulong InstanceId,
    string Signature);   // KV string e.g. "IMPBW:4:10;IMPBH:4:8;"

/// <summary>A skill book entry (first 2 LE bytes of the skill bin + rank byte).</summary>
public sealed record SnapshotSkill(ushort SkillId, byte Rank);

/// <summary>
/// Decoded 0x5209 ChannelCharacterInfoRequestR — the owner-only full entity
/// snapshot. Only the reliably-anchored fields are surfaced; the long
/// version-sensitive tail is summarised by the metadata KV string and master
/// identity. See MabiNotes op_0x5209 for the full field analysis.
/// </summary>
public sealed record ChannelCharacterInfo(
    ulong EntityId,
    string Name,
    uint RaceId,
    uint RegionId,
    uint PosX,
    uint PosY,
    byte Direction,
    float Height,
    float Weight,
    uint Color1,
    uint Color2,
    uint Color3,
    float CombatPower,
    // Stats (idx 32-43 of the TW layout)
    float Life,
    float LifeMax,        // LifeMaxBase + LifeMaxMod
    float Mana,
    float ManaMax,
    float Stamina,
    float StaminaMax,
    // Level / rebirth (idx 44-50)
    ushort Level,
    uint TotalLevelDiff,
    ushort RebirthCount,
    // Primary stats base (idx 51-60)
    float Str,
    float Dex,
    float Int,
    float Will,
    float Luck,
    IReadOnlyList<SnapshotRegen> Regens,
    uint BagWidth,
    uint BagHeight,
    IReadOnlyList<SnapshotItem> Items,
    IReadOnlyList<SnapshotSkill> Skills,
    ulong MasterId,
    string MasterName,
    string Metadata);
