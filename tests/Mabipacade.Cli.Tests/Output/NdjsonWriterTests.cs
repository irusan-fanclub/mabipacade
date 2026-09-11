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
                Direction.Inbound, 0x00006984, 1UL, Array.Empty<MessageElem>(), null));
            writer.WritePacket(new MabiPacket(
                new DateTime(2026, 5, 13, 0, 0, 1, DateTimeKind.Utc),
                Direction.Inbound, 0x00006985, 2UL, Array.Empty<MessageElem>(), null));
        }
        var lines = sb.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("{", lines[0]);
        Assert.EndsWith("}", lines[0]);
    }

    private sealed record NamedThing(string Name, string Markup);

    [Fact]
    public void WritesNonAsciiText_Verbatim()
    {
        // Character, item and quest text is almost entirely CJK. Escaped as
        // \uXXXX it is unreadable in a terminal, a diff, or a log — and the
        // whole point of the decoded output is that a human reads it.
        var sb = new StringBuilder();
        using (var sw = new StringWriter(sb))
        {
            new NdjsonWriter(sw).WritePacket(new MabiPacket(
                DateTime.UnixEpoch, Direction.Inbound, 0x00005209, 1UL,
                new[] { MessageElem.String("蘑菇嫩煎雞") },
                new NamedThing("蘑菇嫩煎雞", "* 經驗值 <color=2>75000</color>")));
        }

        var line = sb.ToString();
        Assert.Contains("蘑菇嫩煎雞", line);
        Assert.DoesNotContain("\\u8611", line);
        // Quest reward text carries markup; escaping it to < helps nobody
        // here, since this output goes to files and terminals, not to HTML.
        Assert.Contains("<color=2>", line);
    }

    [Fact]
    public void Packets_And_Events_PreserveOrder()
    {
        var sb = new StringBuilder();
        using (var sw = new StringWriter(sb))
        {
            var writer = new NdjsonWriter(sw);
            writer.WriteEvent(new SessionEvent.SessionStart(DateTime.UtcNow, "tw", null));
            writer.WritePacket(new MabiPacket(DateTime.UtcNow, Direction.Inbound, 0x00006984, 0UL, Array.Empty<MessageElem>(), null));
            writer.WriteEvent(new SessionEvent.SessionEnd(DateTime.UtcNow, "UserStop"));
        }
        var lines = sb.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.Contains("\"kind\":\"event\"", lines[0]);
        Assert.Contains("\"kind\":\"packet\"", lines[1]);
        Assert.Contains("\"kind\":\"event\"", lines[2]);
    }
}
