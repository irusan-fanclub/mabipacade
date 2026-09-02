using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Entity;
using static Mabipacade.Decoders.Tests.TestSupport.NestedBodyBytes;

namespace Mabipacade.Decoders.Tests.Entity;

public class EntitiesAppearDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x00005334, new EntitiesAppearDecoder().Op);
    }

    [Fact]
    public void Decodes_CharacterEntry_IdNameRace()
    {
        // Captured shape (mogugi logs, 2026-04/05): entry type 16, nested body
        // elems = { Long id, Byte 5, String name, String, String, Int raceId, … }.
        var nested = NestedBody(
            NLong(0x0010F000001CA88EUL), NByte(5),
            NString("_Pinkoasis"), NString(""), NString(""), NInt(2055));
        var input = Input(
            MessageElem.Short(1),
            MessageElem.Short(16), MessageElem.Int((uint)nested.Length), MessageElem.Bin(nested));

        var decoded = Assert.IsType<EntitiesAppear>(new EntitiesAppearDecoder().Decode(input));

        Assert.Equal(1, decoded.DeclaredCount);
        var e = Assert.Single(decoded.Entries);
        Assert.Equal((ushort)16, e.EntryType);
        Assert.Equal(0x0010F000001CA88EUL, e.EntityId);
        Assert.Equal("_Pinkoasis", e.Name);
        Assert.Equal(2055u, e.RaceId);
    }

    [Fact]
    public void Decodes_NonCharacterEntry_IdOnly()
    {
        // Entry type 160 carries { Long id, Int classId, … } — no Byte-5 marker,
        // so only the id is surfaced.
        var nested = NestedBody(NLong(45317531380613121UL), NInt(41907));
        var input = Input(
            MessageElem.Short(1),
            MessageElem.Short(160), MessageElem.Int((uint)nested.Length), MessageElem.Bin(nested));

        var decoded = Assert.IsType<EntitiesAppear>(new EntitiesAppearDecoder().Decode(input));

        var e = Assert.Single(decoded.Entries);
        Assert.Equal((ushort)160, e.EntryType);
        Assert.Equal(45317531380613121UL, e.EntityId);
        Assert.Null(e.Name);
        Assert.Null(e.RaceId);
    }

    [Fact]
    public void Decodes_EmptyBody_NoEntries()
    {
        var input = Input();
        var decoded = Assert.IsType<EntitiesAppear>(new EntitiesAppearDecoder().Decode(input));
        Assert.Equal(0, decoded.DeclaredCount);
        Assert.Empty(decoded.Entries);
    }

    [Fact]
    public void Decodes_MalformedNestedBody_EntryKeptWithZeroId()
    {
        var input = Input(
            MessageElem.Short(1),
            MessageElem.Short(16), MessageElem.Int(3), MessageElem.Bin(new byte[] { 1, 2, 3 }));

        var decoded = Assert.IsType<EntitiesAppear>(new EntitiesAppearDecoder().Decode(input));

        var e = Assert.Single(decoded.Entries);
        Assert.Equal((ushort)16, e.EntryType);
        Assert.Equal(0UL, e.EntityId);
    }

    private static DecoderInput Input(params MessageElem[] elems) =>
        new(DateTime.UtcNow, Direction.Inbound, 0x00005334, 0UL, elems);
}
