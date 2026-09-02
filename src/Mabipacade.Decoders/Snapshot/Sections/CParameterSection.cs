using Mabipacade.Core.Model;

namespace Mabipacade.Decoders.Snapshot.Sections;

/// <summary>
/// Reads <c>pleione::CParameter</c> (<c>CCharacter+0x0F8</c>, vslot 20
/// <c>0x140B64770</c>), the first and largest section of the body.
///
/// Its length is deterministic even though the two stat passes' split is not:
///
///   1  dataType
/// + 26 named prefix fields
/// + 178 positional stat slots (elements 29-206, id = index - 4)
/// + 1  regen count
/// + 7 × regenCount
/// + 8  feature 0x790 floats
///
/// which on the reference capture is 242 elements — exactly the 2-243 span the
/// analysis establishes from the CTitleMgr boundary.
/// </summary>
internal static class CParameterSection
{
    /// <summary>0x5209 sends dataType 2 (Private); 0x520C sends 5 (Public) with a different layout.</summary>
    public const byte PrivateDataType = 2;

    /// <summary>Element index of the first positional stat slot.</summary>
    private const int StatBlockStart = 29;

    /// <summary>Element index of the last positional stat slot; the regen count follows it.</summary>
    private const int StatBlockEnd = 206;

    /// <summary>
    /// Gap between element index and stat id. Cross-validated: mogugi-stat's
    /// statSnapshotIdOffset and the research's element/id table agree, and the
    /// level anchor lands on both (element 44 - 4 = id 40 = Level = 200).
    /// </summary>
    private const int StatIdOffset = 4;

    /// <summary>Feature <c>0x790</c> (<c>G26S2@Taiwan</c>, ON) appends eight floats.</summary>
    private const int Feature0x790FloatCount = 8;

    public static CharacterParameter Read(ElemCursor c)
    {
        byte dataType = c.U8();
        if (dataType != PrivateDataType)
            throw new SnapshotFormatException(c.Index - 1,
                $"CParameter dataType {dataType}, expected {PrivateDataType} (Private)");

        string name = c.Str();
        string title = c.Str();
        string engTitle = c.Str();
        uint raceId = c.U32();
        byte skinColor = c.U8();
        ushort eyeType = c.U16();
        byte eyeColor = c.U8();
        ushort mouthType = c.U16();
        uint status = c.U32();
        float scaleHeight = c.F32();
        float scaleFatness = c.F32();
        float scaleUpper = c.F32();
        float scaleLower = c.F32();
        uint regionId = c.U32();
        uint posX = c.U32();
        uint posY = c.U32();
        sbyte direction = c.I8();
        uint battleState = c.U32();
        byte weaponSet = c.U8();
        uint extra1 = c.U32();
        uint extra2 = c.U32();
        uint extra3 = c.U32();
        float combatPower = c.F32();
        string motionType = c.Str();
        byte oddEyeLeft = c.U8();
        byte oddEyeRight = c.U8();

        var stats = ReadStatBlock(c);
        var regens = ReadRegens(c);

        var extraFloats = new float[Feature0x790FloatCount];
        for (int i = 0; i < extraFloats.Length; i++) extraFloats[i] = c.F32();

        return new CharacterParameter(
            dataType, name, title, engTitle, raceId,
            skinColor, eyeType, eyeColor, mouthType, status,
            scaleHeight, scaleFatness, scaleUpper, scaleLower,
            regionId, posX, posY, direction, battleState, weaponSet,
            extra1, extra2, extra3, combatPower, motionType,
            oddEyeLeft, oddEyeRight,
            stats, regens, extraFloats);
    }

    /// <summary>
    /// Elements 29-206 are one slot per stat id, in id order. Slots whose wire
    /// type is not numeric — id 155 carries a string — are skipped without
    /// shifting the ids after them, so the block is walked by position, never by
    /// matching types.
    /// </summary>
    private static Dictionary<ushort, double> ReadStatBlock(ElemCursor c)
    {
        if (c.Index != StatBlockStart)
            throw new SnapshotFormatException(c.Index,
                $"CParameter prefix ended at {c.Index}, expected the stat block at {StatBlockStart}");

        var stats = new Dictionary<ushort, double>(StatBlockEnd - StatBlockStart + 1);
        for (int i = StatBlockStart; i <= StatBlockEnd; i++)
        {
            var el = c.Any();
            if (TryNumeric(el, out double v))
                stats[(ushort)(i - StatIdOffset)] = v;
        }
        return stats;
    }

    private static List<SnapshotRegen> ReadRegens(ElemCursor c)
    {
        uint count = c.U32();
        var regens = new List<SnapshotRegen>((int)count);
        for (uint i = 0; i < count; i++)
        {
            uint id = c.U32();
            float change = c.F32();
            int timeLeft = c.I32();
            uint stat = c.U32();
            c.U8();              // always 0 on the reference capture
            float max = c.F32();
            c.U8();              // TW trailing byte
            regens.Add(new SnapshotRegen(id, change, timeLeft, stat, max));
        }
        return regens;
    }

    private static bool TryNumeric(MessageElem el, out double value)
    {
        switch (el.Type)
        {
            case MessageElemType.Byte: value = el.AsByte(); return true;
            case MessageElemType.Short: value = el.AsUInt16(); return true;
            case MessageElemType.Int: value = el.AsUInt32(); return true;
            case MessageElemType.Long: value = el.AsUInt64(); return true;
            case MessageElemType.Float: value = el.AsFloat(); return true;
            default: value = 0; return false;
        }
    }
}
