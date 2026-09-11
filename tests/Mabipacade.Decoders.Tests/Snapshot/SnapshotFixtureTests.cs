using Mabipacade.Core.Model;

namespace Mabipacade.Decoders.Tests.Snapshot;

public class SnapshotFixtureTests
{
    [Fact]
    public void Loads_TheDocumentedElementCount()
    {
        Assert.Equal(23976, SnapshotFixture.Elems.Count);
    }

    [Fact]
    public void Loads_TheDocumentedTypeDistribution()
    {
        // From research/packet-0x5209/alignment-0805.md. Matching every count
        // proves the loader reconstructs types faithfully — a Long read back as
        // an Int would shift the whole sequential parse.
        var byType = SnapshotFixture.Elems
            .GroupBy(e => e.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(6937, byType[MessageElemType.Byte]);
        Assert.Equal(4783, byType[MessageElemType.String]);
        Assert.Equal(4242, byType[MessageElemType.Int]);
        Assert.Equal(3127, byType[MessageElemType.Long]);
        Assert.Equal(2415, byType[MessageElemType.Bin]);
        Assert.Equal(1972, byType[MessageElemType.Short]);
        Assert.Equal(500, byType[MessageElemType.Float]);
    }

    [Fact]
    public void Loads_TheDocumentedHeaderAndAnchorValues()
    {
        var e = SnapshotFixture.Elems;

        // Header: u8 result = 1, u64 characterId (00-header.md).
        Assert.Equal((byte)1, e[0].AsByte());
        Assert.Equal(4503599630207674UL, e[1].AsUInt64());

        // CParameter dataType = 2 (Private), then CName (01-cparameter.md).
        Assert.Equal((byte)2, e[2].AsByte());
        Assert.Equal("蘑菇嫩煎雞", e[3].AsString());

        // Anchors quoted throughout the analysis.
        Assert.Equal(10002u, e[6].AsUInt32());        // CType (race)
        Assert.Equal(3100u, e[16].AsUInt32());        // CRegionId
        Assert.Equal((ushort)200, e[44].AsUInt16());  // CLevel
        Assert.Equal(4u, e[207].AsUInt32());          // regen count
    }
}
