using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.Json;
using Mabipacade.Core.Model;

namespace Mabipacade.DebugUi.Services;

// Writes received packets to a JSON-Lines file on a background thread so the UI
// thread is never blocked on disk I/O. One packet per line; safe to tail or grep.
public sealed class PacketLogger : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly object _gate = new();
    private BlockingCollection<MabiPacket>? _queue;
    private Task? _writer;
    private StreamWriter? _stream;
    private CancellationTokenSource? _cts;
    private long _written;

    public bool IsActive { get; private set; }
    public string? CurrentPath { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public long PacketsWritten => Interlocked.Read(ref _written);

    public event EventHandler? StateChanged;

    // Build the default log path: <logsDir>/yyyy-MM-dd_HH-mm-ss.jsonl
    public static string BuildDefaultPath(string logsDir, DateTime startedLocal)
    {
        var name = startedLocal.ToString("yyyy-MM-dd_HH-mm-ss") + ".jsonl";
        return Path.Combine(logsDir, name);
    }

    // Begin a new log. Creates parent directory if missing. Throws on I/O failure.
    public void Start(string path)
    {
        lock (_gate)
        {
            if (IsActive) throw new InvalidOperationException("Logger already active");
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            _stream = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
            _queue = new BlockingCollection<MabiPacket>(boundedCapacity: 8192);
            _cts = new CancellationTokenSource();
            _written = 0;
            CurrentPath = path;
            StartedAtUtc = DateTime.UtcNow;
            IsActive = true;
            var queue = _queue;
            var stream = _stream;
            var token = _cts.Token;
            _writer = Task.Run(() => DrainLoop(queue, stream, token));
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    // Enqueue a packet for writing. Drops if the queue is closed or full.
    public void Append(MabiPacket p)
    {
        var q = _queue;
        if (q is null || q.IsAddingCompleted) return;
        try { q.TryAdd(p); } catch (InvalidOperationException) { /* completed concurrently */ }
    }

    // Flush remaining packets and close the file. Idempotent.
    public void Stop()
    {
        BlockingCollection<MabiPacket>? queue;
        Task? writer;
        StreamWriter? stream;
        CancellationTokenSource? cts;
        lock (_gate)
        {
            if (!IsActive) return;
            queue = _queue;
            writer = _writer;
            stream = _stream;
            cts = _cts;
            _queue = null;
            _writer = null;
            _stream = null;
            _cts = null;
            IsActive = false;
        }

        // Drain order: CompleteAdding → wait writer (which flushes in its finally) → dispose stream.
        // No timeout on Wait: a premature Dispose races the writer mid-WriteLine and truncates the
        // final JSON line. Queue is bounded at 8192, so worst-case drain is bounded.
        try { queue?.CompleteAdding(); } catch { /* already completed */ }
        try { writer?.Wait(); } catch { /* swallow */ }
        try { stream?.Dispose(); } catch { /* swallow */ }
        try { queue?.Dispose(); } catch { /* swallow */ }
        try { cts?.Dispose(); } catch { /* swallow */ }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    // Copy the current (open or closed) log file to a target path. While active,
    // the source is briefly flushed so the copy is consistent up to that moment.
    public void CopyTo(string targetPath)
    {
        string source;
        lock (_gate)
        {
            if (string.IsNullOrEmpty(CurrentPath))
                throw new InvalidOperationException("No log to save");
            source = CurrentPath!;
            try { _stream?.Flush(); } catch { /* best-effort */ }
        }
        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        // FileShare.ReadWrite on source so we can copy while writer holds it.
        using var src = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var dst = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        src.CopyTo(dst);
    }

    public void Dispose() => Stop();

    private void DrainLoop(BlockingCollection<MabiPacket> q, StreamWriter w, CancellationToken token)
    {
        try
        {
            foreach (var p in q.GetConsumingEnumerable(token))
            {
                var line = JsonSerializer.Serialize(LogRecord.From(p), JsonOpts);
                w.WriteLine(line);
                Interlocked.Increment(ref _written);
            }
        }
        catch (OperationCanceledException) { /* normal stop */ }
        catch (Exception)
        {
            // Best-effort: swallow so logger failure never crashes the app.
            // Future: route to a diagnostics channel.
        }
        finally
        {
            try { w.Flush(); } catch { /* swallow */ }
        }
    }

    private readonly record struct LogRecord(
        string T,
        string Dir,
        string Op,
        uint OpDec,
        string Eid,
        string? Type,
        IReadOnlyList<ElemRecord> Elems,
        object? Decoded,
        string? Body)
    {
        public static LogRecord From(MabiPacket p) => new(
            T: p.TimestampUtc.ToString("O"),
            Dir: p.Direction == Direction.Inbound ? "in" : "out",
            Op: $"0x{p.Op:X4}",
            OpDec: p.Op,
            Eid: p.EntityId.ToString(),
            Type: p.Decoded?.GetType().Name,
            Elems: BuildElems(p.Elems),
            Decoded: p.Decoded,
            Body: p.Body is { Length: > 0 } b ? Convert.ToHexString(b) : null);

        private static IReadOnlyList<ElemRecord> BuildElems(IReadOnlyList<MessageElem> elems)
        {
            var list = new List<ElemRecord>(elems.Count);
            for (int i = 0; i < elems.Count; i++)
            {
                var e = elems[i];
                object v = e.Type switch
                {
                    MessageElemType.Byte => e.AsByte(),
                    MessageElemType.Short => e.AsUInt16(),
                    MessageElemType.Int => e.AsUInt32(),
                    MessageElemType.Long => e.AsUInt64().ToString(),
                    MessageElemType.Float => e.AsFloat(),
                    MessageElemType.String => e.AsString(),
                    MessageElemType.Bin => Convert.ToHexString(e.AsBytes()),
                    _ => "?",
                };
                list.Add(new ElemRecord(e.Type.ToString(), v));
            }
            return list;
        }
    }

    private readonly record struct ElemRecord(string T, object V);
}
