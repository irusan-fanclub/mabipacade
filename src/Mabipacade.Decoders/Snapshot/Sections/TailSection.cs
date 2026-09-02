namespace Mabipacade.Decoders.Snapshot.Sections;

/// <summary>
/// The inline fields and feature blocks <c>CCharacter::ProcessDataMessage</c>
/// reads after the quest log, plus the residue behind them.
/// </summary>
internal static class TailSection
{
    /// <summary>
    /// Ends the extra-storage loop of feature <c>0x190</c>
    /// (<c>G12S1@Taiwan</c>, on). Seeing this value land exactly where the
    /// analysis predicted is what confirmed the whole tail's alignment.
    /// </summary>
    private const uint StorageSentinel = 0x49;

    public static SnapshotTail Read(ElemCursor c)
    {
        byte b1 = c.U8();
        ulong t1 = c.U64();
        ulong t2 = c.U64();
        string s1 = c.Str();
        byte b2 = c.U8();
        byte b3 = c.U8();
        ulong t3 = c.U64();

        // Feature 0x190: (pocketId, value) pairs until the sentinel pocket id.
        var storage = new List<(uint PocketId, ulong Value)>();
        while (true)
        {
            uint pocketId = c.U32();
            if (pocketId == StorageSentinel) break;
            storage.Add((pocketId, c.U64()));
        }

        uint f47cId = c.U32();              // feature 0x47C (G19S1@Taiwan)
        ulong f47cValue = c.U64();

        bool f4afFlag = c.Bool();           // feature 0x4AF (base G20S1)
        uint f4afValue = c.U32();

        ulong unconditional = c.U64();

        uint f4eb1 = c.U32();               // feature 0x4EB (base G20S2)
        uint f4eb2 = c.U32();

        byte f50d = c.U8();                 // feature 0x50D (G20S2@Taiwan)

        ulong prTime = c.U64();             // property bag PRTIME

        // ProcessDataMessage stops one ReadBool after PRTIME, but the server
        // sends three more elements. The client simply ignores them, so they are
        // recorded rather than interpreted — and in particular the last element
        // cannot be used to infer whether feature 0x79E is on.
        var residue = new List<Core.Model.MessageElem>();
        while (!c.AtEnd) residue.Add(c.Any());

        return new SnapshotTail(
            new[] { b1, b2, b3 },
            new[] { t1, t2, t3 },
            s1,
            storage.Select(p => new ExtraStorage(p.PocketId, p.Value)).ToList(),
            f47cId, f47cValue, f4afFlag, f4afValue, unconditional,
            f4eb1, f4eb2, f50d, prTime, residue);
    }
}
