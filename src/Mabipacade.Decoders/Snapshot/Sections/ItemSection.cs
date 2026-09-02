using System.Buffers.Binary;
using Mabipacade.Core.Model;

namespace Mabipacade.Decoders.Snapshot.Sections;

/// <summary>
/// The item container (<c>CCharacter::LoadItems</c> <c>0x141C11AC0</c>) and each
/// record inside it (<c>pleione::CItem::Deserialize</c> <c>0x140B7BBD0</c>).
/// At 39% of the packet this is its largest block, and it cannot be skipped:
/// the container loop is driven purely by <c>count</c>, with no length prefix
/// and no sentinel, so the only way past the items is through them.
/// </summary>
internal static class ItemSection
{
    /// <summary>0x5209 only ever sends full snapshots; mode 1 is the partial-update layout.</summary>
    private const byte SnapshotMode = 2;

    public static ItemContainer Read(ElemCursor c)
    {
        uint count = c.U32();
        var items = new List<SnapshotItem>((int)count);
        for (uint i = 0; i < count; i++)
            items.Add(ReadItem(c));
        return new ItemContainer(items);
    }

    private static SnapshotItem ReadItem(ElemCursor c)
    {
        ulong instanceId = c.U64();

        byte mode = c.U8();
        if (mode != SnapshotMode)
            throw new SnapshotFormatException(c.Index - 1,
                $"Item mode {mode}, expected {SnapshotMode} (full snapshot)");

        byte[] core = c.Bin();                      // 80 bytes -> [item+0x10]

        // The one place inside a record where the client dispatches on the tag
        // rather than on position: a non-Bin here means an extra string sits
        // between the core and extended blobs (3 of 804 records).
        string? interstitial = null;
        if (c.PeekType() != MessageElemType.Bin)
            interstitial = c.Str();

        byte[] extended = c.Bin();                  // 144 bytes -> [item+0x70]
        string attributes1 = c.Str();               // KEY:type:value; pairs
        string attributes2 = c.Str();

        byte n1 = c.U8();
        var attachments = new List<byte[]>(n1);
        for (int i = 0; i < n1; i++) attachments.Add(c.Bin());   // 40 bytes each

        ulong questRef = c.U64();
        // QSTTIP is read only when the preceding u64 is non-zero — the one
        // conditional here that the wire itself decides (116 of 804 records).
        string? questTip = questRef != 0 ? c.Str() : null;

        // The remaining branches turn on client-side item state we cannot see,
        // but each alternative starts with a distinct tag, so the tag picks the
        // path: Int = the order-12 pair list, Short = the feature 0x6FC list,
        // Long = the trailing owner id that ends every record.
        var pairs = new List<(uint, uint)>();
        if (c.PeekType() == MessageElemType.Int)
        {
            uint n2 = c.U32();
            for (uint i = 0; i < n2; i++) pairs.Add((c.U32(), c.U32()));
        }

        // Feature 0x435 (G18S8@Taiwan, ON) contributes these two flags to every
        // record. With 804 items, this feature alone moves the packet by 1,608
        // elements — the layout's single most feature-sensitive point.
        bool flag1 = c.Bool();
        bool flag2 = c.Bool();

        var extras = new List<(string, uint)>();
        if (c.PeekType() == MessageElemType.Short)
        {
            ushort n3 = c.U16();                    // count also stored in bag MQTC
            for (int i = 0; i < n3; i++) extras.Add((c.Str(), c.U32()));
        }

        ulong ownerId = c.U64();                    // unconditional; ends the record

        return new SnapshotItem(
            instanceId,
            ItemIdOf(core),
            RecordTypeOf(core),
            QuantityOf(core),
            PosXOf(core),
            PosYOf(core),
            core,
            extended,
            attributes1,
            attributes2,
            attachments,
            questRef,
            questTip,
            interstitial,
            pairs.Select(p => new ItemPair(p.Item1, p.Item2)).ToList(),
            flag1,
            flag2,
            extras.Select(p => new ItemExtra(p.Item1, p.Item2)).ToList(),
            ownerId);
    }

    // Offsets into the 80-byte core. The reference analysis leaves this blob's
    // internals unsolved; these five come from MabiNotes and are the fields the
    // previous decoder surfaced. Treated as best-effort: the blob is kept whole
    // above so a corrected offset needs no re-capture.
    private static uint RecordTypeOf(byte[] core) => U32(core, 0);
    private static uint ItemIdOf(byte[] core) => U32(core, 4);
    private static uint QuantityOf(byte[] core) => U32(core, 36);
    private static uint PosXOf(byte[] core) => U32(core, 44);
    private static uint PosYOf(byte[] core) => U32(core, 48);

    private static uint U32(byte[] b, int offset) =>
        b.Length >= offset + 4 ? BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(offset, 4)) : 0;
}
