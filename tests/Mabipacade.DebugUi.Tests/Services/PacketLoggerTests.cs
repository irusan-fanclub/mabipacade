using System.IO;
using Mabipacade.Core.Model;
using Mabipacade.DebugUi.Services;

namespace Mabipacade.DebugUi.Tests.Services;

public class PacketLoggerTests
{
    private static MabiPacket Pkt(uint op = 0x00007926, ulong eid = 42, params MessageElem[] elems) =>
        new(DateTime.UtcNow, Direction.Inbound, op, eid, elems, null);

    [Fact]
    public void BuildDefaultPath_FormatsTimestampInFilename()
    {
        var dt = new DateTime(2026, 6, 9, 14, 5, 30);
        var p = PacketLogger.BuildDefaultPath("C:/tmp", dt);
        Assert.EndsWith("2026-06-09_14-05-30.jsonl", p);
    }

    [Fact]
    public void StartAppendStop_WritesOneLinePerPacket()
    {
        var temp = NewTempDir();
        try
        {
            var path = Path.Combine(temp, "out.jsonl");
            using var log = new PacketLogger();
            log.Start(path);
            log.Append(Pkt(elems: new[] { MessageElem.Short(7) }));
            log.Append(Pkt(op: 0x0000526C, eid: 99, elems: new[] { MessageElem.String("hi") }));
            log.Stop();

            var lines = File.ReadAllLines(path);
            Assert.Equal(2, lines.Length);
            Assert.Contains("\"op\":\"0x00007926\"", lines[0]);
            Assert.Contains("\"op\":\"0x0000526C\"", lines[1]);
            Assert.Contains("\"hi\"", lines[1]);
        }
        finally { Directory.Delete(temp, true); }
    }

    [Fact]
    public void Start_TwiceWithoutStop_Throws()
    {
        var temp = NewTempDir();
        try
        {
            using var log = new PacketLogger();
            log.Start(Path.Combine(temp, "a.jsonl"));
            Assert.Throws<InvalidOperationException>(() => log.Start(Path.Combine(temp, "b.jsonl")));
            log.Stop();
        }
        finally { Directory.Delete(temp, true); }
    }

    [Fact]
    public void Start_CreatesMissingDirectory()
    {
        var temp = NewTempDir();
        try
        {
            var nested = Path.Combine(temp, "deep", "logs");
            var path = Path.Combine(nested, "x.jsonl");
            using var log = new PacketLogger();
            log.Start(path);
            log.Stop();
            Assert.True(File.Exists(path));
        }
        finally { Directory.Delete(temp, true); }
    }

    [Fact]
    public void CopyTo_DuplicatesActiveLogContents()
    {
        var temp = NewTempDir();
        try
        {
            var src = Path.Combine(temp, "src.jsonl");
            var dst = Path.Combine(temp, "copy.jsonl");
            using var log = new PacketLogger();
            log.Start(src);
            log.Append(Pkt());
            log.Append(Pkt());
            log.Stop(); // flush before copy to make assertion deterministic
            log.CopyTo(dst);

            Assert.Equal(File.ReadAllText(src), File.ReadAllText(dst));
            Assert.Equal(2, File.ReadAllLines(dst).Length);
        }
        finally { Directory.Delete(temp, true); }
    }

    [Fact]
    public void CopyTo_WithoutStart_Throws()
    {
        var temp = NewTempDir();
        try
        {
            using var log = new PacketLogger();
            Assert.Throws<InvalidOperationException>(() => log.CopyTo(Path.Combine(temp, "x.jsonl")));
        }
        finally { Directory.Delete(temp, true); }
    }

    [Fact]
    public void Stop_DrainsQueueFully_NoTruncatedLine()
    {
        // Regression: previously Stop() used a 5s Wait timeout that could race
        // the writer mid-WriteLine, leaving the final JSON line truncated.
        var temp = NewTempDir();
        try
        {
            var path = Path.Combine(temp, "drain.jsonl");
            using var log = new PacketLogger();
            log.Start(path);
            const int n = 2000;
            for (int i = 0; i < n; i++) log.Append(Pkt(op: (uint)(0x1000 + (i & 0xFF))));
            log.Stop();

            var lines = File.ReadAllLines(path);
            Assert.Equal(n, lines.Length);
            foreach (var ln in lines)
            {
                // Every line must be a complete, parseable JSON object.
                var doc = System.Text.Json.JsonDocument.Parse(ln);
                Assert.True(doc.RootElement.TryGetProperty("op", out _));
            }
        }
        finally { Directory.Delete(temp, true); }
    }

    [Fact]
    public void Stop_IsIdempotent()
    {
        var temp = NewTempDir();
        try
        {
            using var log = new PacketLogger();
            log.Start(Path.Combine(temp, "x.jsonl"));
            log.Stop();
            log.Stop();
            Assert.False(log.IsActive);
        }
        finally { Directory.Delete(temp, true); }
    }

    [Fact]
    public void State_Properties_TrackLifecycle()
    {
        var temp = NewTempDir();
        try
        {
            using var log = new PacketLogger();
            Assert.False(log.IsActive);
            Assert.Null(log.CurrentPath);

            var raised = 0;
            log.StateChanged += (_, _) => raised++;

            var path = Path.Combine(temp, "x.jsonl");
            log.Start(path);
            Assert.True(log.IsActive);
            Assert.Equal(path, log.CurrentPath);
            Assert.NotNull(log.StartedAtUtc);

            log.Stop();
            Assert.False(log.IsActive);
            Assert.Equal(path, log.CurrentPath); // path is retained for post-stop "Save As"
            Assert.Equal(2, raised);
        }
        finally { Directory.Delete(temp, true); }
    }

    private static string NewTempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), $"mp-log-{Guid.NewGuid():N}");
        Directory.CreateDirectory(d);
        return d;
    }
}
