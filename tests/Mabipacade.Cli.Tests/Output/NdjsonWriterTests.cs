using System.Text;
using Mabipacade.Core.Json;
using Mabipacade.Core.Diagnostics;
using Mabipacade.Core.Model;

namespace Mabipacade.Cli.Tests.Output;

public class NdjsonWriterTests
{
    [Fact]
    public void WritesOneLinePerPacket_WithTrailingNewline()
    {
        var sb = new StringBuilder();
        using (var sw = new StringWriter(sb))
        {
            var writer = new NdjsonWriter(sw);
            writer.WritePacket(new MabiPacket(
                new DateTime(2026, 5, 13, 0, 0, 0, DateTimeKind.Utc),
                Direction.Inbound, 0x6984, 1UL, Array.Empty<MessageElem>(), null));
            writer.WritePacket(new MabiPacket(
                new DateTime(2026, 5, 13, 0, 0, 1, DateTimeKind.Utc),
                Direction.Inbound, 0x6985, 2UL, Array.Empty<MessageElem>(), null));
        }
        var lines = sb.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("{", lines[0]);
        Assert.EndsWith("}", lines[0]);
    }

    [Fact]
    public void Packets_And_Events_PreserveOrder()
    {
        var sb = new StringBuilder();
        using (var sw = new StringWriter(sb))
        {
            var writer = new NdjsonWriter(sw);
            writer.WriteEvent(new SessionEvent.SessionStart(DateTime.UtcNow, "tw", null));
            writer.WritePacket(new MabiPacket(DateTime.UtcNow, Direction.Inbound, 0x6984, 0UL, Array.Empty<MessageElem>(), null));
            writer.WriteEvent(new SessionEvent.SessionEnd(DateTime.UtcNow, "UserStop"));
        }
        var lines = sb.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.Contains("\"kind\":\"event\"", lines[0]);
        Assert.Contains("\"kind\":\"packet\"", lines[1]);
        Assert.Contains("\"kind\":\"event\"", lines[2]);
    }
}
