namespace Mabipacade.Decoders.Snapshot.Sections;

/// <summary>
/// The fixed-shape components between <c>CParameter</c> and the item container.
/// Every one of them is short, and together they are the alignment bridge into
/// the 39%-of-packet item block, so each is verified against the reference
/// capture's element ranges.
/// </summary>
internal static class HeadSections
{
    /// <summary>
    /// <c>pleione::CTitleMgr</c> (<c>CCharacter+0x118</c>, vslot 3). Four title
    /// ids in two pairs: the style pair is cosmetic, the equipped pair (property
    /// bags <c>MCDT1</c>/<c>MCDT2</c>) actually applies its effects.
    /// Element count = <c>6 + 3 × count</c> at mode 1.
    /// </summary>
    public static TitleManager ReadTitleMgr(ElemCursor c)
    {
        uint styleTitleId = c.U32();
        ulong timestamp = c.U64();
        ushort count = c.U16();

        var titles = new List<CharacterTitle>(count);
        for (int i = 0; i < count; i++)
        {
            uint id = c.U32();
            byte flag = c.U8();     // 0 = locked, 1 = usable, 3 = usable with an expiry
            ulong expires = c.U64();
            titles.Add(new CharacterTitle(id, flag, expires));
        }

        uint styleSubTitleId = c.U32();
        uint equippedTitleId = c.U32();
        uint equippedSubTitleId = c.U32();

        return new TitleManager(styleTitleId, styleSubTitleId,
            equippedTitleId, equippedSubTitleId, timestamp, titles);
    }

    /// <summary>
    /// <c>pleione::CMateMgr</c> (<c>CCharacter+0x120</c>, vslot 5) — the marriage
    /// partner. Mode 1 is four fixed elements with no loop or feature gate, which
    /// makes it the cleanest alignment anchor in the packet.
    /// </summary>
    public static MateInfo ReadMateMgr(ElemCursor c)
    {
        ulong mateId = c.U64();
        string mateName = c.Str();
        ulong unknown = c.U64();
        ushort unknown2 = c.U16();
        return new MateInfo(mateId, mateName, unknown, unknown2);
    }

    /// <summary>
    /// <c>CJobComponent</c> (<c>CCharacter+0x320</c>). One byte, present only
    /// while feature <c>0x14A</c> (<c>gfJobSystem</c>, base G13S1) is on — it is
    /// on for TW. The value indexes <c>data/db/Job.xml</c> (0-8).
    /// </summary>
    public static byte ReadJob(ElemCursor c) => c.U8();

    /// <summary>
    /// <c>COptionWeaponComponent</c> (<c>CCharacter+0x480</c>, reached through
    /// <c>vf+0x680</c> — a virtual call, which is why the first pass of the
    /// analysis missed it entirely). Three lists; the third is gated on feature
    /// <c>0x675</c> (base G25S3, on for TW).
    /// Element count = <c>4 + 3×n1 + n2 + 1 + 2×n3</c>.
    /// </summary>
    public static OptionWeapon ReadOptionWeapon(ElemCursor c)
    {
        ushort head1 = c.U16();
        uint head2 = c.U32();

        uint n1 = c.U32();
        var slots = new List<OptionWeaponSlot>((int)n1);
        for (uint i = 0; i < n1; i++)
            slots.Add(new OptionWeaponSlot(c.U16(), c.U16(), c.U16()));

        uint n2 = c.U32();
        var ids = new List<uint>((int)n2);
        for (uint i = 0; i < n2; i++) ids.Add(c.U32());

        uint n3 = c.U32();
        var levels = new List<OptionWeaponLevel>((int)n3);
        for (uint i = 0; i < n3; i++)
            levels.Add(new OptionWeaponLevel(c.U32(), c.U16()));

        return new OptionWeapon(head1, head2, slots, ids, levels);
    }

    /// <summary>
    /// Feature <c>0x659</c> (base G25S2, on for TW) — one u32 written to the
    /// property bag named <c>SCMO</c>.
    /// </summary>
    public static uint ReadScmo(ElemCursor c) => c.U32();

    /// <summary>
    /// Feature <c>0x1A4</c> (<c>G13S1EX@Taiwan</c>, on) — the character's own bag
    /// dimensions. Note the client still reads both elements when the values are
    /// zero; only the feature being off removes them from the wire.
    /// </summary>
    public static InventorySize ReadInventorySize(ElemCursor c)
        => new(c.U32(), c.U32());
}
