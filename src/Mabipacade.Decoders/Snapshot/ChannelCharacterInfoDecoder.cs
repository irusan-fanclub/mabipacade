using System.Buffers.Binary;
using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Snapshot;

/// <summary>
/// Decoder for 0x5209 ChannelCharacterInfoRequestR, the owner-only full entity
/// snapshot (600-700 elems). The fixed prefix (idx 0-60) is read by index; the
/// dynamic sections (regens, inventory, skill book) are located structurally so
/// the parser tolerates the version-sensitive tail. Everything is defensive —
/// a malformed packet yields a partially-populated record, never an exception.
/// </summary>
public sealed class ChannelCharacterInfoDecoder : IPacketDecoder
{
    public uint Op => 0x00005209;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;

        // --- Fixed prefix (TW layout, idx 0-60) ---------------------------
        ulong entityId = Long(e, 1);
        string name = Str(e, 3);
        uint raceId = Int(e, 6);
        float height = Flt(e, 12);
        float weight = Flt(e, 13);
        uint region = Int(e, 16);
        uint posX = Int(e, 17);
        uint posY = Int(e, 18);
        byte dir = Byte(e, 19);
        uint c1 = Int(e, 22) & 0xFFFFFF;
        uint c2 = Int(e, 23) & 0xFFFFFF;
        uint c3 = Int(e, 24) & 0xFFFFFF;
        float cp = Flt(e, 25);

        // Stats: Life, LifeInjured, LifeMaxBase, LifeMaxMod, Mana, ManaMaxBase,
        // ManaMaxMod, Stamina, StaminaMaxBase, StaminaMaxMod, Hunger, sentinel.
        float life = Flt(e, 32);
        float lifeMax = Flt(e, 34) + Flt(e, 35);
        float mana = Flt(e, 36);
        float manaMax = Flt(e, 37) + Flt(e, 38);
        float stamina = Flt(e, 39);
        float staminaMax = Flt(e, 40) + Flt(e, 41);

        ushort level = Short(e, 44);
        uint totalLevelDiff = Int(e, 45);
        ushort rebirth = Short(e, 47);

        float str = Flt(e, 51);
        float dex = Flt(e, 53);
        float intel = Flt(e, 55);
        float will = Flt(e, 57);
        float luck = Flt(e, 59);

        // --- Regen list (count at idx 207 of the fixed prefix) ------------
        var regens = new List<SnapshotRegen>();
        int idx = 208;
        if (e.Count > 207 && e[207].Type == MessageElemType.Int)
        {
            uint regenCount = e[207].AsUInt32();
            for (uint r = 0; r < regenCount && idx + 6 < e.Count; r++, idx += 7)
            {
                if (e[idx].Type != MessageElemType.Int) break;
                regens.Add(new SnapshotRegen(
                    Id: Int(e, idx),
                    Change: Flt(e, idx + 1),
                    TimeLeft: unchecked((int)Int(e, idx + 2)),
                    Stat: Int(e, idx + 3),
                    Max: Flt(e, idx + 5)));
            }
        }

        // --- Inventory: scan for the (W, H, N, Long) header ---------------
        uint bagW = 0, bagH = 0;
        var items = new List<SnapshotItem>();
        int bagStart = FindBagHeader(e, idx);
        int afterInventory = bagStart;
        if (bagStart >= 0)
        {
            bagW = Int(e, bagStart);
            bagH = Int(e, bagStart + 1);
            uint n = Int(e, bagStart + 2);
            int p = bagStart + 3;
            for (uint i = 0; i < n && p + 6 < e.Count; i++)
            {
                if (e[p].Type != MessageElemType.Long) break;
                ulong inst = Long(e, p);
                byte[] core = Bin(e, p + 2);
                string sig = Str(e, p + 4);
                int attN = e.Count > p + 6 && e[p + 6].Type == MessageElemType.Byte ? e[p + 6].AsByte() : 0;
                items.Add(BuildItem(inst, core, sig));
                p += 11 + attN; // fixed 11 elems + attN attachment bins
            }
            afterInventory = p;
        }

        // --- Skill book: Short keywordCount, keywords, Short skillCount, bins
        var skills = new List<SnapshotSkill>();
        int sp = afterInventory;
        if (sp >= 0 && sp + 1 < e.Count && e[sp].Type == MessageElemType.Short)
        {
            int keywordCount = e[sp].AsUInt16();
            sp += 1 + keywordCount;            // skip keyword shorts
            if (sp < e.Count && e[sp].Type == MessageElemType.Short)
            {
                int skillCount = e[sp].AsUInt16();
                sp += 1;
                for (int s = 0; s < skillCount && sp < e.Count; s++, sp++)
                {
                    if (e[sp].Type != MessageElemType.Bin) break;
                    var b = e[sp].AsBytes();
                    if (b.Length >= 5)
                        skills.Add(new SnapshotSkill(
                            (ushort)(b[0] | (b[1] << 8)), b[4]));
                }
            }
        }

        // --- Master identity + KV metadata (scan the tail) ----------------
        (ulong masterId, string masterName, string metadata) = ScanTail(e, sp, name, entityId);

        return new ChannelCharacterInfo(
            entityId, name, raceId, region, posX, posY, dir,
            height, weight, c1, c2, c3, cp,
            life, lifeMax, mana, manaMax, stamina, staminaMax,
            level, totalLevelDiff, rebirth,
            str, dex, intel, will, luck,
            regens, bagW, bagH, items, skills,
            masterId, masterName, metadata);
    }

    // Scan forward for a plausible inventory header: Int w (1-12), Int h
    // (1-20), Int n (0-300), followed by a Long item id when n > 0.
    private static int FindBagHeader(IReadOnlyList<MessageElem> e, int from)
    {
        for (int i = Math.Max(0, from); i + 3 < e.Count; i++)
        {
            if (e[i].Type != MessageElemType.Int ||
                e[i + 1].Type != MessageElemType.Int ||
                e[i + 2].Type != MessageElemType.Int) continue;
            uint w = e[i].AsUInt32(), h = e[i + 1].AsUInt32(), n = e[i + 2].AsUInt32();
            if (w == 0 || w > 12 || h == 0 || h > 20 || n > 300) continue;
            if (n == 0) return i; // empty bag, no Long follows
            if (e[i + 3].Type == MessageElemType.Long && e[i + 3].AsUInt64() > 1_000_000_000UL)
                return i;
        }
        return -1;
    }

    private static SnapshotItem BuildItem(ulong inst, byte[] core, string sig)
    {
        uint U(int o) => core.Length >= o + 4 ? BinaryPrimitives.ReadUInt32LittleEndian(core.AsSpan(o, 4)) : 0;
        return new SnapshotItem(
            RecType: U(0),
            ItemId: U(4),
            Quantity: U(36),
            PosX: U(44),
            PosY: U(48),
            InstanceId: inst,
            Signature: sig);
    }

    // Entity IDs live in a high band (~4.5e15); FILETIMEs (~6.4e13) and small
    // counters must not be mistaken for the master EID.
    private const ulong EntityIdMin = 4_000_000_000_000_000UL;
    private const ulong EntityIdMax = 5_000_000_000_000_000UL;

    // From the post-skillbook tail: the master EID is the most frequent
    // entity-range Long that isn't the snapshot's own id; the master name is the
    // first non-empty plain String; the metadata is the longest KV blob.
    private static (ulong, string, string) ScanTail(
        IReadOnlyList<MessageElem> e, int from, string selfName, ulong selfId)
    {
        var longCounts = new Dictionary<ulong, int>();
        string masterName = "";
        string metadata = "";
        for (int i = Math.Max(0, from); i < e.Count; i++)
        {
            var el = e[i];
            if (el.Type == MessageElemType.Long)
            {
                ulong v = el.AsUInt64();
                if (v >= EntityIdMin && v <= EntityIdMax && v != selfId)
                    longCounts[v] = longCounts.GetValueOrDefault(v) + 1;
            }
            else if (el.Type == MessageElemType.String)
            {
                var s = el.AsString();
                if (s.Length == 0) continue;
                if (masterName.Length == 0 && s != selfName && !s.Contains(':') && s.Length < 32)
                    masterName = s;
                if (s.Length > metadata.Length && s.Contains(';') && s.Contains(':'))
                    metadata = s;
            }
        }
        ulong masterId = 0;
        int best = 0;
        foreach (var kv in longCounts)
            if (kv.Value > best) { best = kv.Value; masterId = kv.Key; }
        return (masterId, masterName, metadata);
    }

    // --- Guarded element accessors ---------------------------------------
    private static ulong Long(IReadOnlyList<MessageElem> e, int i) =>
        i >= 0 && i < e.Count && e[i].Type == MessageElemType.Long ? e[i].AsUInt64() : 0;
    private static uint Int(IReadOnlyList<MessageElem> e, int i) =>
        i >= 0 && i < e.Count && e[i].Type == MessageElemType.Int ? e[i].AsUInt32() : 0;
    private static ushort Short(IReadOnlyList<MessageElem> e, int i) =>
        i >= 0 && i < e.Count && e[i].Type == MessageElemType.Short ? e[i].AsUInt16() : (ushort)0;
    private static byte Byte(IReadOnlyList<MessageElem> e, int i) =>
        i >= 0 && i < e.Count && e[i].Type == MessageElemType.Byte ? e[i].AsByte() : (byte)0;
    private static float Flt(IReadOnlyList<MessageElem> e, int i) =>
        i >= 0 && i < e.Count && e[i].Type == MessageElemType.Float ? e[i].AsFloat() : 0f;
    private static string Str(IReadOnlyList<MessageElem> e, int i) =>
        i >= 0 && i < e.Count && e[i].Type == MessageElemType.String ? e[i].AsString() : "";
    private static byte[] Bin(IReadOnlyList<MessageElem> e, int i) =>
        i >= 0 && i < e.Count && e[i].Type == MessageElemType.Bin ? e[i].AsBytes() : Array.Empty<byte>();
}
