using System.Text.Json;
using Mabipacade.Core.Json;
using Mabipacade.Core.Diagnostics;
using Mabipacade.Core.Model;
using Mabipacade.Decoders;
using System.Net;

namespace Mabipacade.Cli.Tests.Output;

public class EnvelopeShapeTests
{
    private static string RenderPacket(MabiPacket p)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            EnvelopeShape.WritePacket(w, p, OpCodeNames.TryGetName);
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string RenderEvent(SessionEvent ev)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            EnvelopeShape.WriteEvent(w, ev);
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    [Fact]
    public void Packet_ShapeAndFields()
    {
        var ts = new DateTime(2026, 5, 13, 8, 23, 11, 842, DateTimeKind.Utc);
        var p = new MabiPacket(ts, Direction.Inbound, 0x00006984, 12345UL,
            new[] { MessageElem.Short(59000) }, Decoded: null);
        var json = RenderPacket(p);

        Assert.Contains("\"kind\":\"packet\"", json);
        Assert.Contains("\"ts\":\"2026-05-13T08:23:11.842Z\"", json);
        Assert.Contains("\"dir\":\"in\"", json);
        Assert.Contains("\"op\":\"0x00006984\"", json);
        Assert.Contains("\"opName\":\"PlayerSkillPrepareStart\"", json);
        Assert.Contains("\"entityId\":\"12345\"", json);
        Assert.Contains("\"type\":null", json);
        Assert.Contains("\"decoded\":null", json);
        Assert.Contains("\"elems\":[", json);
    }

    [Fact]
    public void Packet_WithDecoded_IncludesTypeAndPayload()
    {
        var ts = DateTime.UtcNow;
        var pocoLike = new { skillId = 59000 };
        var p = new MabiPacket(ts, Direction.Inbound, 0x00006984, 1UL,
            Array.Empty<MessageElem>(), Decoded: pocoLike);
        var json = RenderPacket(p);

        Assert.Contains("\"type\":\"", json);
        Assert.Contains("\"decoded\":{\"skillId\":59000}", json);
    }

    [Fact]
    public void Packet_UnknownOp_OpNameNull()
    {
        var p = new MabiPacket(DateTime.UtcNow, Direction.Inbound, 0x0000FFFF, 0UL,
            Array.Empty<MessageElem>(), null);
        var json = RenderPacket(p);
        Assert.Contains("\"opName\":null", json);
    }

    [Fact]
    public void Event_SessionStart_ShapeAndFields()
    {
        var ts = new DateTime(2026, 5, 13, 8, 23, 11, DateTimeKind.Utc);
        var ev = new SessionEvent.SessionStart(ts, "tw", 4812);
        var json = RenderEvent(ev);

        Assert.Contains("\"kind\":\"event\"", json);
        Assert.Contains("\"type\":\"SessionStart\"", json);
        Assert.Contains("\"region\":\"tw\"", json);
        Assert.Contains("\"processId\":4812", json);
    }

    [Fact]
    public void Event_ConnectionResumed_IncludesSameAsLast()
    {
        var ev = new SessionEvent.ConnectionResumed(DateTime.UtcNow,
            new IPEndPoint(IPAddress.Parse("61.218.1.2"), 11000), SameAsLast: false);
        var json = RenderEvent(ev);

        Assert.Contains("\"type\":\"ConnectionResumed\"", json);
        Assert.Contains("\"newRemote\":\"61.218.1.2:11000\"", json);
        Assert.Contains("\"sameAsLast\":false", json);
    }

    [Fact]
    public void Event_BadBody_IncludesOpAndLength()
    {
        var ev = new SessionEvent.BadBody(DateTime.UtcNow, 0x00009093, 42);
        var json = RenderEvent(ev);
        Assert.Contains("\"type\":\"BadBody\"", json);
        Assert.Contains("\"op\":\"0x00009093\"", json);
        Assert.Contains("\"length\":42", json);
    }
}
