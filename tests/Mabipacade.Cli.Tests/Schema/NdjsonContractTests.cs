using System.Text;
using System.Text.Json;
using Mabipacade.Core.Json;
using Mabipacade.Core.Diagnostics;
using Mabipacade.Core.Model;
using System.Net;

namespace Mabipacade.Cli.Tests.Schema;

public class NdjsonContractTests
{
    private static string RenderPackets(params MabiPacket[] packets)
    {
        var sb = new StringBuilder();
        using var sw = new StringWriter(sb);
        var writer = new NdjsonWriter(sw);
        foreach (var p in packets) writer.WritePacket(p);
        return sb.ToString();
    }

    [Fact]
    public void Packet_HasRequiredFields()
    {
        var output = RenderPackets(new MabiPacket(
            new DateTime(2026, 5, 13, 8, 0, 0, DateTimeKind.Utc),
            Direction.Inbound, 0x6984, 12345UL,
            new[] { MessageElem.Short(59000) }, null));
        var line = output.TrimEnd();
        var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        foreach (var required in new[] { "kind", "ts", "dir", "op", "opName", "entityId", "type", "decoded", "elems" })
            Assert.True(root.TryGetProperty(required, out _), $"missing required field: {required}");

        Assert.Equal("packet", root.GetProperty("kind").GetString());
        Assert.Equal("in", root.GetProperty("dir").GetString());
        Assert.Equal("0x6984", root.GetProperty("op").GetString());
        Assert.Equal("12345", root.GetProperty("entityId").GetString());
    }

    [Fact]
    public void Event_HasRequiredFields()
    {
        var sb = new StringBuilder();
        using var sw = new StringWriter(sb);
        var writer = new NdjsonWriter(sw);
        writer.WriteEvent(new SessionEvent.ConnectionResumed(DateTime.UtcNow,
            new IPEndPoint(IPAddress.Parse("1.2.3.4"), 11000), SameAsLast: false));
        var line = sb.ToString().TrimEnd();
        var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        foreach (var required in new[] { "kind", "ts", "type", "newRemote", "sameAsLast" })
            Assert.True(root.TryGetProperty(required, out _), $"missing required field: {required}");

        Assert.Equal("event", root.GetProperty("kind").GetString());
        Assert.Equal("ConnectionResumed", root.GetProperty("type").GetString());
        Assert.False(root.GetProperty("sameAsLast").GetBoolean());
    }

    [Fact]
    public void Elem_UsesTV_Compact()
    {
        var output = RenderPackets(new MabiPacket(
            DateTime.UtcNow, Direction.Inbound, 0x6984, 0UL,
            new[] { MessageElem.Short(42) }, null));
        var doc = JsonDocument.Parse(output.TrimEnd());
        var elem = doc.RootElement.GetProperty("elems")[0];
        Assert.True(elem.TryGetProperty("t", out _));
        Assert.True(elem.TryGetProperty("v", out _));
    }

    [Fact]
    public void UnknownOp_OpNameIsNull_NotMissing()
    {
        var output = RenderPackets(new MabiPacket(
            DateTime.UtcNow, Direction.Inbound, 0xFFFF, 0UL,
            Array.Empty<MessageElem>(), null));
        var doc = JsonDocument.Parse(output.TrimEnd());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("opName").ValueKind);
    }
}
