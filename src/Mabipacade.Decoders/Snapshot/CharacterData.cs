using Mabipacade.Core.Model;

namespace Mabipacade.Decoders.Snapshot;

/// <summary>
/// Where one component's data sat in the element array. The 0x5209 body has no
/// length prefixes, so a section's span is the parser's primary claim: getting
/// it wrong misaligns everything after. Sections whose field meanings are not
/// yet established still report an exact span and keep their raw elements.
/// </summary>
public sealed record SnapshotSection(
    string Name,
    int Start,
    int Length,
    IReadOnlyList<MessageElem> Raw);

/// <summary>
/// Decoded <c>0x5209 NET_CHARACTER_DATA_REPLY</c> — the owner-only full
/// snapshot the server pushes as the second-to-last step of login.
///
/// The body is a chain of component sections with no length prefixes, so
/// parsing is strictly sequential and stops at the first section that does not
/// line up. <see cref="Sections"/> records what was consumed and
/// <see cref="Error"/> says why it stopped, so a layout change after a game
/// update degrades to a short-but-honest result instead of silent nonsense.
/// </summary>
public sealed record CharacterSnapshot(
    // 1 = success; 0x66 = silently ignored by the client; anything else is an error.
    byte Result,
    ulong CharacterId,
    SnapshotBody Body,
    IReadOnlyList<SnapshotSection> Sections,
    // Element index reached. Equals the element count when the packet parsed whole.
    int ParsedThrough,
    int ElementCount,
    // Null when every section parsed; otherwise why alignment was lost.
    string? Error)
{
    public bool IsComplete => Error is null && ParsedThrough == ElementCount;
}

/// <summary>
/// The components decoded from the body, filled in as the chain is walked. A
/// section left null was never reached — parsing stops at the first misaligned
/// component, and everything before it stays valid.
/// </summary>
public sealed class SnapshotBody
{
    public CharacterParameter? Parameter { get; set; }
    public TitleManager? Titles { get; set; }
    public MateInfo? Mate { get; set; }
    public byte? JobId { get; set; }
    public OptionWeapon? OptionWeapon { get; set; }
    public uint? Scmo { get; set; }
    public InventorySize? InventorySize { get; set; }
    public ItemContainer? Items { get; set; }

    public IReadOnlyList<ushort>? Keywords { get; set; }
    public SkillManager? Skills { get; set; }
    public IReadOnlyList<CharacterCondition>? Conditions { get; set; }
    public GuildInfo? Guild { get; set; }
    public PetInfo? Pet { get; set; }
    public AchievementInfo? Achievements { get; set; }
    public IReadOnlyList<PrivateFarmEntry>? PrivateFarm { get; set; }
    public FamilyInfo? Family { get; set; }
    public TalentInfo? Talent { get; set; }
    public IReadOnlyList<ShapeShiftEntry>? ShapeShifts { get; set; }
    public EgoWeaponInfo? EgoWeapons { get; set; }
    public MultiClassInfo? MultiClass { get; set; }
    public IReadOnlyList<IReadOnlyList<AstrologistEntry>>? Astrologist { get; set; }
    public IReadOnlyList<ShapeShiftEntry>? MusicBuffSharing { get; set; }
    public IReadOnlyList<QuestEntry>? Quests { get; set; }
    public SnapshotTail? Tail { get; set; }

    // Components whose fields are not established are reachable through
    // CharacterSnapshot.Sections, which keeps every section's raw elements.
}

/// <summary>
/// One entry of the title list. <paramref name="Flag"/>: 0 = locked,
/// 1 = usable, 3 = usable with an expiry — and only flag 3 carries a non-zero
/// <paramref name="ExpiresAt"/>, which held for all 363 entries on the
/// reference capture.
/// </summary>
public sealed record CharacterTitle(uint Id, byte Flag, ulong ExpiresAt);

/// <summary>
/// <c>pleione::CTitleMgr</c>. The style pair is cosmetic; the equipped pair goes
/// into the <c>MCDT1</c>/<c>MCDT2</c> property bags, which is where the effect
/// system reads from — so those two are the ones that change your stats.
/// </summary>
public sealed record TitleManager(
    uint StyleTitleId,
    uint StyleSubTitleId,
    uint EquippedTitleId,
    uint EquippedSubTitleId,
    ulong Timestamp,
    IReadOnlyList<CharacterTitle> Titles);

/// <summary><c>pleione::CMateMgr</c> — the marriage partner. Field meanings past the name are unverified.</summary>
public sealed record MateInfo(ulong MateId, string MateName, ulong Unknown1, ushort Unknown2);

public sealed record OptionWeaponSlot(ushort Key, ushort Value1, ushort Value2);

public sealed record OptionWeaponLevel(uint Id, ushort Level);

/// <summary><c>COptionWeaponComponent</c>. Field meanings are unverified; the shape and length are not.</summary>
public sealed record OptionWeapon(
    ushort Head1,
    uint Head2,
    IReadOnlyList<OptionWeaponSlot> Slots,
    IReadOnlyList<uint> Ids,
    IReadOnlyList<OptionWeaponLevel> Levels);

/// <summary>The character's own bag dimensions, from feature <c>0x1A4</c>.</summary>
public sealed record InventorySize(uint Width, uint Height);

public sealed record ItemPair(uint Value1, uint Value2);

public sealed record ItemExtra(string Key, uint Value);

/// <summary>
/// One record from <c>pleione::CItem::Deserialize</c>. The two binary blobs are
/// kept whole: the reference analysis has not solved their internals, so the
/// five named fields below are MabiNotes offsets into <see cref="Core"/> and may
/// be corrected without needing a fresh capture.
/// </summary>
public sealed record SnapshotItem(
    ulong InstanceId,
    uint ItemId,
    uint RecordType,
    uint Quantity,
    uint PosX,
    uint PosY,
    // 80 bytes -> [item+0x10].
    byte[] Core,
    // 144 bytes -> [item+0x70].
    byte[] Extended,
    // Both are "KEY:type:value;" runs, sharing the client's property-bag namespace.
    string Attributes1,
    string Attributes2,
    // 40 bytes each; upgrade / trait slots, unconfirmed.
    IReadOnlyList<byte[]> Attachments,
    ulong QuestRef,
    // Property bag QSTTIP, present only when QuestRef is non-zero.
    string? QuestTip,
    // Set on the rare records that carry a string between the two blobs.
    string? Interstitial,
    IReadOnlyList<ItemPair> Pairs,
    bool Flag1,
    bool Flag2,
    // Property bags FK_BD / IMEQN, gated on feature 0x6FC.
    IReadOnlyList<ItemExtra> Extras,
    // The holding character's entity id, identical for every item in a snapshot.
    ulong OwnerId);

public sealed record ItemContainer(IReadOnlyList<SnapshotItem> Items);

// --- Components between the item container and the quest log ---------------

public sealed record SkillBonus(uint Id, float Value);

/// <summary>Skill book entries are fixed 76-byte blobs whose internals are unsolved.</summary>
public sealed record SkillManager(
    IReadOnlyList<byte[]> Skills,
    IReadOnlyList<SkillBonus> Bonuses,
    byte Tail1,
    uint Tail2);

public sealed record BannerInfo(byte Flag, string Text);

/// <summary>A buff or debuff. <paramref name="CasterId"/> of ulong.MaxValue means none.</summary>
public sealed record CharacterCondition(
    uint Id,
    ulong CasterId,
    string Parameters,
    ulong Unknown,
    string Extra1,
    string Extra2);

public sealed record GuildInfo(
    ulong Id,
    string Name,
    string Motto,
    uint Color,
    IReadOnlyList<byte> Emblem,
    IReadOnlyList<byte> EmblemColors,
    uint Unknown1,
    uint Unknown2,
    ulong Unknown3);

public sealed record ArbeitEntry(uint Value1, uint Value2, uint Value3);

public sealed record ArbeitInfo(ulong Head, IReadOnlyList<ArbeitEntry> Entries);

public sealed record SummonSlave(ulong Id, byte Flag1, byte Flag2, float Value);

public sealed record TransformInfo(byte Mode, ushort Id1, ushort Id2, uint Value1, uint Value2);

public sealed record PetInfo(
    string Name,
    string Tail,
    uint Unknown,
    IReadOnlyList<ulong> Ids,
    IReadOnlyList<byte> Flags);

public sealed record TameInfo(ulong Id, byte Flag1, byte Flag2, byte Flag3, uint Value);

public sealed record VehicleInfo(uint V1, uint V2, ulong V3, uint V4, byte B1, byte B2, byte B3);

public sealed record ShowdownInfo(uint Value, ulong Id1, ulong Id2, byte Flag);

public sealed record TransportInfo(ulong Id, uint Value);

public sealed record EventInfo(byte Flag1, byte Flag2, string Text, byte Flag3, bool Enabled);

public sealed record AchievementInfo(uint Points, IReadOnlyList<ushort> Ids);

public sealed record PrivateFarmEntry(
    ulong Id, uint Value, ushort Value2, ushort Value3, string Name, string Extra);

public sealed record FamilyInfo(
    ulong Id, string Name, ushort Value1, ushort Value2, ushort Value3, string MemberName);

public sealed record CommerceInfo(byte Flag, ulong Id, float Value, uint Count);

/// <summary>
/// <c>core::ITalentComponent</c>. <paramref name="SelectedTitleId"/> is the
/// currently-worn talent title — the value MabiNotes saw twice in this packet
/// without an explanation.
/// </summary>
public sealed record TalentInfo(
    ushort SelectedTitleId,
    byte GrandmasterTalentId,
    IReadOnlyList<uint> Experience,
    IReadOnlyList<ushort> OwnedTitleIds);

/// <summary>Also used for the music-buff sharing list, which has the same triple shape.</summary>
public sealed record ShapeShiftEntry(uint Id, byte Value1, byte Value2);

public sealed record EgoWeapon(
    ulong ItemId,
    string Name,
    // "<id>:<level>" pairs — the ego weapon's ability levels.
    string Options,
    string Extra,
    ulong Timestamp,
    IReadOnlyList<uint> Unknowns);

public sealed record EgoWeaponInfo(IReadOnlyList<ushort> Head, IReadOnlyList<EgoWeapon> Weapons);

public sealed record ClassRecord(ushort ClassId, uint Experience, ushort ArcanaLevel);

public sealed record MultiClassInfo(
    ushort CurrentClassId,
    IReadOnlyList<ClassRecord> Classes,
    IReadOnlyList<string> Trailing);

public sealed record AstrologistEntry(ushort Id, IReadOnlyList<ushort> Values);

// --- Quest log and tail ----------------------------------------------------

/// <summary><c>core::CQuestDesc</c> — the quest's text and classification.</summary>
public sealed record QuestDescription(
    // Selects the objective layout, so it must be read before the objectives.
    byte QuestType,
    uint QuestClassId,
    string Name,
    string Description,
    string Extra,
    string Extra2,
    // "<xml soundset=... npc=.../>"
    string SoundSet,
    IReadOnlyList<ushort> List1,
    IReadOnlyList<ushort> List2,
    IReadOnlyList<uint> Unknowns);

public sealed record QuestObjective(
    byte Kind,
    string Description,
    // "TARGETCOUNT:4:1;TGTCLS:4:701021;"
    string Condition,
    string Extra,
    uint Value,
    float Progress,
    byte Bits,
    IReadOnlyList<string> TypeExtras,
    bool Flag,
    uint Trailing);

/// <summary><paramref name="Amount"/> is present only for reward kind 26.</summary>
public sealed record QuestReward(byte Kind, string Text, byte Value, byte Bits, uint? Amount);

public sealed record QuestRewardGroup(
    byte Flag1, byte Flag2, bool Enabled, IReadOnlyList<QuestReward> Rewards);

public sealed record QuestEntry(
    ulong QuestId,
    QuestDescription Description,
    // QMxxx coefficients, e.g. "QMSMGLD:f:1;QMBEXP:f:1;"
    string Coefficients,
    IReadOnlyList<QuestObjective> Objectives,
    IReadOnlyList<QuestRewardGroup> RewardGroups,
    ulong Unknown1,
    uint Unknown2,
    bool Trailing);

/// <summary>One entry of the feature <c>0x190</c> extra-storage list.</summary>
public sealed record ExtraStorage(uint PocketId, ulong Value);

public sealed record SnapshotTail(
    IReadOnlyList<byte> Bytes,
    IReadOnlyList<ulong> Timestamps,
    string Text,
    IReadOnlyList<ExtraStorage> Storage,
    uint Feature47CId,
    ulong Feature47CValue,
    bool Feature4AFFlag,
    uint Feature4AFValue,
    ulong Unconditional,
    uint Feature4EB1,
    uint Feature4EB2,
    byte Feature50D,
    // Property bag PRTIME.
    ulong PrTime,
    // Elements the server sends past the client's last read; not interpreted.
    IReadOnlyList<MessageElem> Residue);

/// <summary>A natural-recovery entry: stat 28/32/35/38 = life/mana/stamina/hunger.</summary>
public sealed record SnapshotRegen(
    uint Id,
    float Change,
    int TimeLeft,
    uint Stat,
    float Max);

/// <summary>
/// <c>pleione::CParameter</c> — the character's numeric state, and the largest
/// contributor to the packet's head. Sent as two passes over the same 215-entry
/// stat array (group masks 4 then 8), so a stat belonging to both groups appears
/// twice. The split point between the passes is not resolvable from the wire
/// alone (the masks are runtime globals), which is why the named fields below
/// stop where the evidence does and the rest is exposed positionally.
/// </summary>
public sealed record CharacterParameter(
    byte DataType,
    string Name,
    string Title,
    string EngTitle,
    uint RaceId,
    byte SkinColor,
    ushort EyeType,
    byte EyeColor,
    ushort MouthType,
    uint Status,
    float ScaleHeight,
    float ScaleFatness,
    float ScaleUpper,
    float ScaleLower,
    uint RegionId,
    uint PosX,
    uint PosY,
    sbyte Direction,
    uint BattleState,
    byte WeaponSet,
    uint Extra1,
    uint Extra2,
    uint Extra3,
    float CombatPower,
    string MotionType,
    byte OddEyeLeftColor,
    byte OddEyeRightColor,
    // Positional stat block, keyed by stat id: id = elementIndex - 4, the one
    // rule two independent sources agree on (mogugi-stat's statSnapshotIdOffset
    // and the research's element/id table). Non-numeric slots — id 155 carries a
    // string — are absent from the map but still occupy their slot, so the ids
    // after them never shift.
    IReadOnlyDictionary<ushort, double> Stats,
    IReadOnlyList<SnapshotRegen> Regens,
    // Feature 0x790 (G26S2@Taiwan, ON) appends these eight floats.
    IReadOnlyList<float> ExtraFloats);
