using System.Text.Json;
using Mabipacade.Core.Model;
using Mabipacade.DebugUi.Services;

namespace Mabipacade.DebugUi.Tests.Services;

public class PacketExportBuilderTests
{
    private static MabiPacket Packet(uint op) =>
        new(new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc), Direction.Inbound,
            op, 7UL, new[] { MessageElem.Short(42) }, Decoded: null);

    [Fact]
    public void SuggestedStem_NamesByCountAndOp()
    {
        Assert.Equal("packet-0x00005209", PacketExportBuilder.SuggestedStem(new[] { Packet(0x5209) }));
        Assert.Equal("packets-0x00005209-x3",
            PacketExportBuilder.SuggestedStem(new[] { Packet(0x5209), Packet(0x5209), Packet(0x5209) }));
        Assert.Equal("packets-x2",
            PacketExportBuilder.SuggestedStem(new[] { Packet(0x5209), Packet(0x6984) }));
    }

    [Fact]
    public void BuildSlimJson_SinglePacket_StaysASingleObject()
    {
        var json = PacketExportBuilder.BuildSlimJson(new[] { Packet(0x5209) });
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        Assert.Equal(21001u, doc.RootElement.GetProperty("opDec").GetUInt32());
    }

    [Fact]
    public void BuildSlimJson_MultiSelection_BecomesAnArrayInOrder()
    {
        var json = PacketExportBuilder.BuildSlimJson(new[] { Packet(0x5209), Packet(0x6984) });
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(2, doc.RootElement.GetArrayLength());
        Assert.Equal("0x00005209", doc.RootElement[0].GetProperty("op").GetString());
        Assert.Equal("0x00006984", doc.RootElement[1].GetProperty("op").GetString());
    }

    [Fact]
    public void BuildEnvelopeJson_MultiSelection_IsAnArrayOfFullEnvelopes()
    {
        var json = PacketExportBuilder.BuildEnvelopeJson(new[] { Packet(0x5209), Packet(0x6984) });
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal("packet", doc.RootElement[0].GetProperty("kind").GetString());
        Assert.True(doc.RootElement[1].TryGetProperty("decoded", out _));
    }
}
