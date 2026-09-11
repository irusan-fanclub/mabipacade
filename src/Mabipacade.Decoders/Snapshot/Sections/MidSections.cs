namespace Mabipacade.Decoders.Snapshot.Sections;

/// <summary>
/// The component chain between the item container and the quest log.
///
/// Most of these are short fixed-shape records. Where the reference analysis
/// established only a boundary and not the field meanings, the reader still
/// consumes the exact element count and the orchestrator keeps the raw slice —
/// the span is what the sections after it depend on, so it is the part that has
/// to be right. Readers marked "observation-pinned" take their length from the
/// reference capture rather than from a formula in the disassembly; those are
/// the ones most likely to need revisiting on another character.
/// </summary>
internal static class MidSections
{
    /// <summary><c>CKeyword</c> (<c>+0x1D0</c>, vslot 10): <c>u16 count</c> then that many ids.</summary>
    public static IReadOnlyList<ushort> ReadKeywords(ElemCursor c)
    {
        ushort count = c.U16();
        var ids = new List<ushort>(count);
        for (int i = 0; i < count; i++) ids.Add(c.U16());
        return ids;
    }

    /// <summary>
    /// <c>CSkillMgr</c> (<c>+0x108</c>, vslot 70): the skill book as fixed 76-byte
    /// blobs, then a bonus list, then a two-element signature tail.
    /// </summary>
    public static SkillManager ReadSkillMgr(ElemCursor c)
    {
        ushort count = c.U16();
        var skills = new List<byte[]>(count);
        for (int i = 0; i < count; i++) skills.Add(c.Bin());

        uint bonusCount = c.U32();
        var bonuses = new List<SkillBonus>((int)bonusCount);
        for (uint i = 0; i < bonusCount; i++)
            bonuses.Add(new SkillBonus(c.U32(), c.F32()));

        byte tail1 = c.U8();
        uint tail2 = c.U32();
        return new SkillManager(skills, bonuses, tail1, tail2);
    }

    /// <summary><c>CBannerMgr</c> (<c>+0x110</c>, vslot 3).</summary>
    public static BannerInfo ReadBanner(ElemCursor c) => new(c.U8(), c.Str());

    /// <summary>
    /// <c>CPVPMgr</c> (<c>+0x138</c>, vslot 3): 18 fixed elements. The signature
    /// <c>b i b i b b b q q i b b i i i i b b</c> is unique in the packet, which
    /// is how the boundary was fixed. Field meanings are unverified.
    /// </summary>
    public static IReadOnlyList<object> ReadPvp(ElemCursor c)
    {
        var v = new object[18];
        v[0] = c.U8(); v[1] = c.U32(); v[2] = c.U8(); v[3] = c.U32();
        v[4] = c.U8(); v[5] = c.U8(); v[6] = c.U8(); v[7] = c.U64();
        v[8] = c.U64(); v[9] = c.U32(); v[10] = c.U8(); v[11] = c.U8();
        v[12] = c.U32(); v[13] = c.U32(); v[14] = c.U32(); v[15] = c.U32();
        v[16] = c.U8(); v[17] = c.U8();
        return v;
    }

    /// <summary>
    /// <c>CConditionMgr</c> (<c>+0x130</c>, vslot 3) — the buffs and debuffs on
    /// the character. MabiNotes' "section R, structure unknown" is this.
    /// Element count = <c>1 + 6×count + 1</c>.
    /// </summary>
    public static IReadOnlyList<CharacterCondition> ReadConditions(ElemCursor c)
    {
        uint count = c.U32();
        var list = new List<CharacterCondition>((int)count);
        for (uint i = 0; i < count; i++)
        {
            uint id = c.U32();
            ulong casterId = c.U64();     // ulong.MaxValue = no caster
            string parameters = c.Str();  // KEY:type:value;
            ulong unknown = c.U64();
            string extra1 = c.Str();
            string extra2 = c.Str();
            list.Add(new CharacterCondition(id, casterId, parameters, unknown, extra1, extra2));
        }
        c.U64();                          // trailing field, unverified
        return list;
    }

    /// <summary><c>CGuildComponent</c> (<c>+0x148</c>, direct): 14 fixed elements.</summary>
    public static GuildInfo ReadGuild(ElemCursor c)
    {
        ulong id = c.U64();
        string name = c.Str();
        uint unk1 = c.U32();
        uint unk2 = c.U32();
        ulong unk3 = c.U64();
        byte b1 = c.U8(), b2 = c.U8(), b3 = c.U8();
        uint color = c.U32();
        byte c1 = c.U8(), c2 = c.U8(), c3 = c.U8(), c4 = c.U8();
        string motto = c.Str();
        return new GuildInfo(id, name, motto, color,
            new byte[] { b1, b2, b3 }, new byte[] { c1, c2, c3, c4 }, unk1, unk2, unk3);
    }

    /// <summary><c>CArbeitMgr</c> (<c>+0x210</c>, vslot 3): <c>u64</c> then a list of u32 triples.</summary>
    public static ArbeitInfo ReadArbeit(ElemCursor c)
    {
        ulong head = c.U64();
        uint count = c.U32();
        var entries = new List<(uint, uint, uint)>((int)count);
        for (uint i = 0; i < count; i++) entries.Add((c.U32(), c.U32(), c.U32()));
        return new ArbeitInfo(head,
            entries.Select(e => new ArbeitEntry(e.Item1, e.Item2, e.Item3)).ToList());
    }

    /// <summary><c>CSummonSlave</c> (<c>+0x1B0</c>, vslot 3): <c>u64 u8 u8 float</c>.</summary>
    public static SummonSlave ReadSummonSlave(ElemCursor c)
        => new(c.U64(), c.U8(), c.U8(), c.F32());

    /// <summary><c>CSummonMaster</c> (<c>+0x1B8</c>, direct): one <c>u64</c>.</summary>
    public static ulong ReadSummonMaster(ElemCursor c) => c.U64();

    /// <summary><c>CTransformMgr</c> (<c>+0x150</c>, vslot 5): <c>u8 u16 u16 u32 u32</c>.</summary>
    public static TransformInfo ReadTransform(ElemCursor c)
        => new(c.U8(), c.U16(), c.U16(), c.U32(), c.U32());

    /// <summary>
    /// <c>CPetMgr</c> (<c>+0x158</c>, vslot 25), mode-1 branch:
    /// <c>str u32 u64 u64 u8 u8 u64 u8 u64 u64 u8 str</c>.
    /// </summary>
    public static PetInfo ReadPet(ElemCursor c)
    {
        string name = c.Str();
        uint unk1 = c.U32();
        ulong id1 = c.U64(), id2 = c.U64();
        byte b1 = c.U8(), b2 = c.U8();
        ulong id3 = c.U64();
        byte b3 = c.U8();
        ulong id4 = c.U64(), id5 = c.U64();
        byte b4 = c.U8();
        string tail = c.Str();
        return new PetInfo(name, tail, unk1, new[] { id1, id2, id3, id4, id5 },
            new[] { b1, b2, b3, b4 });
    }

    /// <summary><c>CHouseComponent</c> (<c>+0x188</c>, vslot 4): one <c>u64</c>. Boundary derived by elimination.</summary>
    public static ulong ReadHouse(ElemCursor c) => c.U64();

    /// <summary><c>CTameMgr</c> (<c>+0x198</c>, vslot 5): <c>u64 u8 u8 u8 u32</c>.</summary>
    public static TameInfo ReadTame(ElemCursor c)
        => new(c.U64(), c.U8(), c.U8(), c.U8(), c.U32());

    /// <summary>
    /// <c>CVehicle</c> (<c>+0x1A0</c>, vslot 12): <c>u32 u32 u64 u32 u8 u8 u8</c>.
    /// Observation-pinned — the disassembled signature contains a loop, so this
    /// shape is the one the reference capture happens to take.
    /// </summary>
    public static VehicleInfo ReadVehicle(ElemCursor c)
        => new(c.U32(), c.U32(), c.U64(), c.U32(), c.U8(), c.U8(), c.U8());

    /// <summary><c>CShowdownComponent</c> (<c>+0x240</c>, vslot 7): <c>u32 u64 u64 u8</c>.</summary>
    public static ShowdownInfo ReadShowdown(ElemCursor c)
        => new(c.U32(), c.U64(), c.U64(), c.U8());

    /// <summary><c>CTransportComponent</c> (<c>+0x280</c>, vslot 10): <c>u64 u32</c>.</summary>
    public static TransportInfo ReadTransport(ElemCursor c) => new(c.U64(), c.U32());

    /// <summary><c>CAviationComponent</c> (<c>+0x288</c>) and <c>CSkiingComponent</c> (<c>+0x290</c>): one byte each.</summary>
    public static byte ReadSingleByte(ElemCursor c) => c.U8();

    /// <summary>
    /// <c>CFarmingComponent</c> (<c>+0x2A8</c>, vslot 8). The farm id is read
    /// first and a zero short-circuits the rest (<c>0x1426A4972</c>) — the only
    /// component in this group that starts with a u64, which is what identified it.
    /// </summary>
    public static ulong ReadFarming(ElemCursor c)
    {
        ulong farmId = c.U64();
        // A non-zero id would be followed by the farm's own fields; the
        // reference capture has none, so that path is untested.
        return farmId;
    }

    /// <summary><c>CEventComponent</c> (<c>+0x2A0</c>, vslot 10): <c>u8 u8 str u8 bool</c>.</summary>
    public static EventInfo ReadEvent(ElemCursor c)
        => new(c.U8(), c.U8(), c.Str(), c.U8(), c.Bool());

    /// <summary><c>CHeartStickerComponent</c> (<c>+0x2C0</c>, vslot 3): <c>u16 u16</c>.</summary>
    public static (ushort, ushort) ReadHeartSticker(ElemCursor c) => (c.U16(), c.U16());

    /// <summary>
    /// <c>CJoustComponent</c> (<c>+0x2C8</c>, vslot 6):
    /// <c>u32 u32 u8 u8 u8 u16 u16 u16 u16 u32 u32</c>. Observation-pinned.
    /// </summary>
    public static IReadOnlyList<object> ReadJoust(ElemCursor c)
    {
        var v = new object[11];
        v[0] = c.U32(); v[1] = c.U32(); v[2] = c.U8(); v[3] = c.U8();
        v[4] = c.U8(); v[5] = c.U16(); v[6] = c.U16(); v[7] = c.U16();
        v[8] = c.U16(); v[9] = c.U32(); v[10] = c.U32();
        return v;
    }

    /// <summary><c>CAchievement</c> (<c>+0x2E0</c>, vslot 3): <c>u32</c>, <c>u16 count</c>, then ids.</summary>
    public static AchievementInfo ReadAchievements(ElemCursor c)
    {
        uint points = c.U32();
        ushort count = c.U16();
        var ids = new List<ushort>(count);
        for (int i = 0; i < count; i++) ids.Add(c.U16());
        return new AchievementInfo(points, ids);
    }

    /// <summary>
    /// <c>CPrivateFarmComponent</c> (<c>+0x330</c>, vslot 24):
    /// <c>u32 count</c> then <c>count × (u64 u32 u16 u16 str str)</c>.
    /// </summary>
    public static IReadOnlyList<PrivateFarmEntry> ReadPrivateFarm(ElemCursor c)
    {
        uint count = c.U32();
        var list = new List<PrivateFarmEntry>((int)count);
        for (uint i = 0; i < count; i++)
            list.Add(new PrivateFarmEntry(c.U64(), c.U32(), c.U16(), c.U16(), c.Str(), c.Str()));
        return list;
    }

    /// <summary><c>CFamilyComponent</c> (<c>+0x300</c>, vslot 3): id, name, three u16, and a member name.</summary>
    public static FamilyInfo ReadFamily(ElemCursor c)
        => new(c.U64(), c.Str(), c.U16(), c.U16(), c.U16(), c.Str());

    /// <summary><c>CDemiGodComponent</c> (<c>+0x2D0</c>, vslot 5): one <c>u32</c>.</summary>
    public static uint ReadDemiGod(ElemCursor c) => c.U32();

    /// <summary><c>CCommerceComponent</c> (<c>+0x340</c>, vslot 16): <c>u8 u64 float u32</c>.</summary>
    public static CommerceInfo ReadCommerce(ElemCursor c)
        => new(c.U8(), c.U64(), c.F32(), c.U32());

    /// <summary>
    /// <c>CTalentComponent</c> (<c>+0x358</c>, direct). The 24 exp values match the
    /// <c>mov esi,0x18</c> loop in the talent research, and
    /// <c>SelectedTitleId</c> resolves MabiNotes' open question about the value
    /// 12216 appearing twice in this packet.
    /// </summary>
    public static TalentInfo ReadTalent(ElemCursor c)
    {
        ushort selectedTitleId = c.U16();
        byte grandmasterTalentId = c.U8();
        var exp = new uint[24];
        for (int i = 0; i < exp.Length; i++) exp[i] = c.U32();
        byte titleCount = c.U8();
        var titles = new List<ushort>(titleCount);
        for (int i = 0; i < titleCount; i++) titles.Add(c.U16());
        return new TalentInfo(selectedTitleId, grandmasterTalentId, exp, titles);
    }

    /// <summary>
    /// <c>CShapeShiftComponent</c> (<c>+0x360</c>, <c>call [vt+0x18]</c>):
    /// <c>u32 count</c> then triples. This is MabiNotes' unexplained "176 triples
    /// right after the talent block".
    /// </summary>
    public static IReadOnlyList<ShapeShiftEntry> ReadShapeShift(ElemCursor c)
    {
        uint count = c.U32();
        var list = new List<ShapeShiftEntry>((int)count);
        for (uint i = 0; i < count; i++)
            list.Add(new ShapeShiftEntry(c.U32(), c.U8(), c.U8()));
        return list;
    }

    /// <summary>Feature <c>0x395</c> (base G18S1, on): two <c>u32</c> at <c>0x1407D3240</c>.</summary>
    public static (uint, uint) ReadFeature0x395(ElemCursor c) => (c.U32(), c.U32());

    /// <summary><c>CRockPaperScissorsComponent</c> (<c>+0x380</c>): <c>u32 u64 u64 str u8 u32</c>.</summary>
    public static IReadOnlyList<object> ReadRockPaperScissors(ElemCursor c)
        => new object[] { c.U32(), c.U64(), c.U64(), c.Str(), c.U8(), c.U32() };

    /// <summary><c>CRegisterComponent</c> (<c>+0x388</c>): <c>u32 count</c> then <c>(u64, u32)</c> pairs.</summary>
    public static IReadOnlyList<(ulong, uint)> ReadRegister(ElemCursor c)
    {
        uint count = c.U32();
        var list = new List<(ulong, uint)>((int)count);
        for (uint i = 0; i < count; i++) list.Add((c.U64(), c.U32()));
        return list;
    }

    /// <summary>
    /// <c>CNewEgoWeaponComponent</c> (<c>+0x450</c>) — ego weapons. Five u16, then
    /// <c>u32 count</c>, then uniform 17-element records
    /// <c>u32 u16 u16 u16 u64 str u16 u32 u32 u16 u16 u16 str str u16 u32 u64</c>.
    /// The record's name and option string are the readable parts; the option
    /// string is <c>&lt;id&gt;:&lt;level&gt;</c> pairs.
    /// </summary>
    public static EgoWeaponInfo ReadNewEgoWeapon(ElemCursor c)
    {
        var head = new ushort[5];
        for (int i = 0; i < head.Length; i++) head[i] = c.U16();

        uint count = c.U32();
        var weapons = new List<EgoWeapon>((int)count);
        for (uint i = 0; i < count; i++)
        {
            uint f0 = c.U32();
            ushort f1 = c.U16(), f2 = c.U16(), f3 = c.U16();
            ulong itemId = c.U64();
            string name = c.Str();
            ushort f6 = c.U16();
            uint f7 = c.U32(), f8 = c.U32();
            ushort f9 = c.U16(), f10 = c.U16(), f11 = c.U16();
            string options = c.Str();
            string extra = c.Str();
            ushort f14 = c.U16();
            uint f15 = c.U32();
            ulong timestamp = c.U64();
            weapons.Add(new EgoWeapon(itemId, name, options, extra, timestamp,
                new uint[] { f0, f1, f2, f3, f6, f7, f8, f9, f10, f11, f14, f15 }));
        }
        return new EgoWeaponInfo(head, weapons);
    }

    /// <summary>
    /// <c>CMagigraphComponent</c> (<c>+0x470</c>): 27 elements of mixed type.
    /// Observation-pinned — the boundary was derived by elimination between its
    /// neighbours, and no field structure was established, so the span is
    /// consumed opaquely and kept as raw elements.
    /// </summary>
    public const int MagigraphLength = 27;

    /// <summary>
    /// <c>CMultiClassComponent</c> (<c>+0x498</c>) — arcana / multiclass.
    /// <c>u16 currentClassId</c>, <c>u32 count</c>, then
    /// <c>(u16 classId, u32 exp, u16 arcanaLevel)</c>, then four string groups.
    /// </summary>
    public static MultiClassInfo ReadMultiClass(ElemCursor c)
    {
        ushort currentClassId = c.U16();
        uint count = c.U32();
        var classes = new List<ClassRecord>((int)count);
        for (uint i = 0; i < count; i++)
            classes.Add(new ClassRecord(c.U16(), c.U32(), c.U16()));

        // Four repeats of <feature 0x770> String Bool u16, all empty here.
        var trailing = new List<string>();
        for (int i = 0; i < 4; i++)
        {
            trailing.Add(c.Str()); trailing.Add(c.Str());
            trailing.Add(c.Str()); trailing.Add(c.Str());
            c.U8(); c.U16();
        }
        return new MultiClassInfo(currentClassId, classes, trailing);
    }

    /// <summary><c>CPrivateIslandNPCComponent</c> (<c>+0x4B0</c>): one byte; a zero truncates the rest.</summary>
    public static byte ReadPrivateIslandNpc(ElemCursor c) => c.U8();

    /// <summary>
    /// <c>CAstrologistComponent</c> (<c>+0x4B8</c>, feature <c>0x6E2</c>) — three
    /// levels of nesting: <c>u16 groups</c>, each <c>u16 entries</c>, each
    /// <c>u16 id, u16 n, n × u16</c>.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<AstrologistEntry>> ReadAstrologist(ElemCursor c)
    {
        ushort groupCount = c.U16();
        var groups = new List<IReadOnlyList<AstrologistEntry>>(groupCount);
        for (int g = 0; g < groupCount; g++)
        {
            ushort entryCount = c.U16();
            var entries = new List<AstrologistEntry>(entryCount);
            for (int e = 0; e < entryCount; e++)
            {
                ushort id = c.U16();
                ushort n = c.U16();
                var values = new List<ushort>(n);
                for (int v = 0; v < n; v++) values.Add(c.U16());
                entries.Add(new AstrologistEntry(id, values));
            }
            groups.Add(entries);
        }
        return groups;
    }

    /// <summary><c>CRoyalAlchemistComponent</c> (<c>+0x4C0</c>): <c>u8 u8</c>.</summary>
    public static (byte, byte) ReadRoyalAlchemist(ElemCursor c) => (c.U8(), c.U8());

    /// <summary>
    /// <c>CMusicBuffSharingComponent</c> (<c>+0x4C8</c>, feature <c>0x714</c>):
    /// <c>u16 count</c> then <c>(u16 id, u8, u8)</c>.
    /// </summary>
    public static IReadOnlyList<ShapeShiftEntry> ReadMusicBuffSharing(ElemCursor c)
    {
        ushort count = c.U16();
        var list = new List<ShapeShiftEntry>(count);
        for (int i = 0; i < count; i++)
            list.Add(new ShapeShiftEntry(c.U16(), c.U8(), c.U8()));
        return list;
    }
}
