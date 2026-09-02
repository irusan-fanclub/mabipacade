using Mabipacade.Core.Model;

namespace Mabipacade.Decoders.Snapshot.Sections;

/// <summary>
/// <c>pleione::CQuestCtrl</c> (<c>CCharacter+0x1c8</c>, <c>0x141adba20</c>) — the
/// quest log, and at 42% of the packet its largest single section.
///
/// Only <c>mode 0</c> is handled: 0x5209 is the login snapshot and the client
/// hard-codes that mode here. Modes 2 and 3 have a different layout, so reading
/// them with this shape would misalign the rest of the packet rather than merely
/// produce wrong values.
/// </summary>
internal static class QuestSection
{
    private const byte SnapshotMode = 0;

    /// <summary>Reward kind 0x1A is the only one carrying a trailing u32.</summary>
    private const byte RewardKindWithValue = 26;

    public static IReadOnlyList<QuestEntry> Read(ElemCursor c)
    {
        uint count = c.U32();
        var quests = new List<QuestEntry>((int)count);
        for (uint i = 0; i < count; i++) quests.Add(ReadQuest(c));
        return quests;
    }

    private static QuestEntry ReadQuest(ElemCursor c)
    {
        ulong questId = c.U64();

        byte mode = c.U8();
        if (mode != SnapshotMode)
            throw new SnapshotFormatException(c.Index - 1,
                $"Quest mode {mode}, expected {SnapshotMode} (full snapshot)");

        ulong unknown = c.U64();
        var desc = ReadDescription(c);
        string coefficients = c.Str();      // QMxxx KEY:type:value; run

        // A questType jump table sits here. Every type seen on the reference
        // capture lands in the default arm, which reads nothing; types 1, 7, 8,
        // 14, 18 and 24 would add fields and are untested.
        uint unknown2 = c.U32();

        uint objectiveCount = c.U32();
        var objectives = new List<QuestObjective>((int)objectiveCount);
        for (uint i = 0; i < objectiveCount; i++)
            objectives.Add(ReadObjective(c, desc.QuestType));

        byte groupCount = c.U8();
        var groups = new List<QuestRewardGroup>(groupCount);
        for (int i = 0; i < groupCount; i++) groups.Add(ReadRewardGroup(c));

        bool trailing = c.Bool();           // last element of the record

        return new QuestEntry(questId, desc, coefficients, objectives, groups,
            unknown, unknown2, trailing);
    }

    /// <summary>
    /// <c>core::CQuestDesc::Deserialize</c> (<c>0x140610460</c>) — 19 fields plus
    /// two u16-counted id lists, both empty on the reference capture.
    /// </summary>
    private static QuestDescription ReadDescription(ElemCursor c)
    {
        byte questType = c.U8();
        uint questClassId = c.U32();
        string name = c.Str();
        string description = c.Str();
        string extra = c.Str();
        uint u1 = c.U32(), u2 = c.U32(), u3 = c.U32();

        ushort count1 = c.U16();
        var list1 = new List<ushort>(count1);
        for (int i = 0; i < count1; i++) list1.Add(c.U16());

        ushort count2 = c.U16();
        var list2 = new List<ushort>(count2);
        for (int i = 0; i < count2; i++) list2.Add(c.U16());

        ushort u4 = c.U16();
        uint u5 = c.U32(), u6 = c.U32(), u7 = c.U32(), u8 = c.U32();
        string extra2 = c.Str();
        uint u9 = c.U32(), u10 = c.U32();
        string soundSet = c.Str();          // <xml soundset=... npc=.../>

        return new QuestDescription(questType, questClassId, name, description,
            extra, extra2, soundSet, list1, list2,
            new[] { u1, u2, u3, (uint)u4, u5, u6, u7, u8, u9, u10 });
    }

    /// <summary>
    /// One objective (<c>0x140dac100</c>). The branch after the bit field keys on
    /// the quest's type, not the objective's kind — reading it as the kind
    /// matches only 32 of 116 records.
    /// </summary>
    private static QuestObjective ReadObjective(ElemCursor c, byte questType)
    {
        byte kind = c.U8();
        string description = c.Str();
        string condition = c.Str();         // TARGETCOUNT:4:1;TGTCLS:4:...
        string extra = c.Str();
        uint value = c.U32();
        float progress = c.F32();
        byte bits = c.U8();

        var typeExtras = new List<string>();
        switch (questType)
        {
            case 12:
                typeExtras.Add(c.Str());
                c.U32();
                c.Bool();
                break;
            case 25:
                typeExtras.Add(c.Str());
                // Feature 0x636 (G25S2@Taiwan) is on, adding six more fields.
                typeExtras.Add(c.Str());
                typeExtras.Add(c.Str());
                c.U32(); c.U32(); c.U32();
                typeExtras.Add(c.Str());
                break;
            case 30:
            case 32:
                typeExtras.Add(c.Str());
                break;
        }

        bool flag = c.Bool();
        if (flag)
        {
            c.U32(); c.U32(); c.U32(); c.U32();
            c.Str();
        }

        uint trailing = c.U32();            // unconditional, ends the objective

        return new QuestObjective(kind, description, condition, extra,
            value, progress, bits, typeExtras, flag, trailing);
    }

    /// <summary>A reward group (<c>0x140464fa0</c>): three flags, a count, then the rewards.</summary>
    private static QuestRewardGroup ReadRewardGroup(ElemCursor c)
    {
        byte f1 = c.U8(), f2 = c.U8();
        bool enabled = c.Bool();
        byte entryCount = c.U8();

        var rewards = new List<QuestReward>(entryCount);
        for (int i = 0; i < entryCount; i++)
        {
            byte kind = c.U8();
            string text = c.Str();          // e.g. "* 經驗值 <color=2>75000</color>"
            byte v = c.U8();
            byte bits = c.U8();
            uint? amount = kind == RewardKindWithValue ? c.U32() : null;
            rewards.Add(new QuestReward(kind, text, v, bits, amount));
        }
        return new QuestRewardGroup(f1, f2, enabled, rewards);
    }

    /// <summary>
    /// Finds the start of <c>CQuestCtrl</c>. Elements 13753-13798 on the
    /// reference capture belong to <c>CPrestigeCostume</c> / <c>CFocusListen</c> /
    /// <c>CServiceMgr</c>, whose order is known but whose split points are not —
    /// the one place in the packet where sequential reading cannot continue.
    ///
    /// The quest log's opening is distinctive enough to resynchronise on:
    /// <c>u32 count</c> followed by a first record of
    /// <c>u64, u8 mode = 0, u64, u8, u32, str, str, str</c>. That pattern occurs
    /// exactly once in all 23,976 elements of the reference capture, and both
    /// ends of the section it starts are independently confirmed, so this is a
    /// targeted resync at a known gap rather than a general heuristic.
    /// </summary>
    public static int FindQuestCtrl(ElemCursor c, int searchLimit)
    {
        for (int offset = 0; offset <= searchLimit; offset++)
        {
            if (c.PeekType(offset) != MessageElemType.Int) continue;
            if (c.PeekType(offset + 1) != MessageElemType.Long) continue;
            if (c.PeekType(offset + 2) != MessageElemType.Byte) continue;
            if (c.Peek(offset + 2).AsByte() != SnapshotMode) continue;
            if (c.PeekType(offset + 3) != MessageElemType.Long) continue;
            if (c.PeekType(offset + 4) != MessageElemType.Byte) continue;
            if (c.PeekType(offset + 5) != MessageElemType.Int) continue;
            if (c.PeekType(offset + 6) != MessageElemType.String) continue;
            if (c.PeekType(offset + 7) != MessageElemType.String) continue;
            if (c.PeekType(offset + 8) != MessageElemType.String) continue;
            return offset;
        }
        throw new SnapshotFormatException(c.Index,
            $"No CQuestCtrl anchor within {searchLimit} elements of {c.Index}");
    }
}
