using Mabipacade.Decoders.Snapshot;
using Mabipacade.Decoders.Snapshot.Sections;

namespace Mabipacade.Decoders.Tests.Snapshot;

public class CParameterSectionTests
{
    /// <summary>Body starts at element 2; elements 0-1 are the u8/u64 header.</summary>
    private const int BodyStart = 2;

    /// <summary>CTitleMgr starts here, so CParameter must consume exactly 2..243.</summary>
    private const int ExpectedEnd = 244;

    private static (CharacterParameter Param, int End) ReadFixture()
    {
        var c = new ElemCursor(SnapshotFixture.Elems, BodyStart);
        var p = CParameterSection.Read(c);
        return (p, c.Index);
    }

    [Fact]
    public void ConsumesExactly_ThroughElement243()
    {
        // The span the whole rest of the parse depends on: one element too few
        // or too many here and every later section reads the wrong slots.
        var (_, end) = ReadFixture();
        Assert.Equal(ExpectedEnd, end);
    }

    [Fact]
    public void Reads_TheNamedPrefixFields()
    {
        var (p, _) = ReadFixture();

        Assert.Equal(CParameterSection.PrivateDataType, p.DataType);
        Assert.Equal("蘑菇嫩煎雞", p.Name);
        Assert.Equal("", p.Title);
        Assert.Equal(10002u, p.RaceId);
        Assert.Equal((byte)45, p.SkinColor);
        Assert.Equal((ushort)207, p.EyeType);
        Assert.Equal((ushort)80, p.MouthType);
        Assert.Equal(3100u, p.RegionId);
        Assert.Equal(292464u, p.PosX);
        Assert.Equal(346553u, p.PosY);
        Assert.Equal(unchecked((sbyte)227), p.Direction);
        Assert.Equal(8007.6724f, p.CombatPower, 3);
    }

    [Fact]
    public void Reads_TheStatBlock_ByPosition()
    {
        var (p, _) = ReadFixture();

        // Level is the anchor both sources agree on: element 44, stat id 40.
        Assert.Equal(200d, p.Stats[40]);
        // The five primary stats and their equipment mods, ids 47-56.
        Assert.Equal(1847d, p.Stats[47]);
        Assert.Equal(367d, p.Stats[48]);
        Assert.Equal(1959.75d, p.Stats[49]);
        Assert.Equal(2376.25d, p.Stats[51]);
        Assert.Equal(1286.25d, p.Stats[53]);
        Assert.Equal(769.25d, p.Stats[55]);
        // Remaining ability points.
        Assert.Equal(126263d, p.Stats[65]);
    }

    [Fact]
    public void Reads_TheRegenList()
    {
        var (p, _) = ReadFixture();

        Assert.Equal(4, p.Regens.Count);
        // Life / mana / stamina / hunger, per the analysis.
        Assert.Equal(new uint[] { 28, 32, 35, 38 }, p.Regens.Select(r => r.Stat));
        Assert.Equal(1u, p.Regens[0].Id);
        Assert.Equal(0.12f, p.Regens[0].Change, 4);
        Assert.Equal(-0.01f, p.Regens[3].Change, 4);
    }

    [Fact]
    public void Reads_TheFeature0x790Floats()
    {
        var (p, _) = ReadFixture();

        Assert.Equal(8, p.ExtraFloats.Count);
        Assert.All(p.ExtraFloats, f => Assert.Equal(0f, f));
    }
}
