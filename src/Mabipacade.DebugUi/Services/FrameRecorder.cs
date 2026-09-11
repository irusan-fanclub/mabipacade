using System.Collections.Concurrent;
using System.IO;
using Mabipacade.Core.Recording;
using PacketDotNet;

namespace Mabipacade.DebugUi.Services;

/// <summary>
/// Records raw capture frames to a pcapng file on a background thread, so the
/// capture callback is never held up by disk I/O. Mirrors
/// <see cref="PacketLogger"/>, which does the same for decoded packets: a
/// session writes both, one readable by Wireshark and one by grep.
/// </summary>
public sealed class FrameRecorder : IDisposable
{
    private const string Application = "mabipacade DebugUi";

    /// <summary>
    /// Frames are far more frequent than decoded packets, so the queue is deeper
    /// than the logger's. Overflow drops frames rather than stalling the capture
    /// — a gap in the recording beats losing live packets.
    /// </summary>
    private const int QueueCapacity = 32768;

    private readonly object _gate = new();
    private BlockingCollection<(byte[] Frame, DateTime Timestamp)>? _queue;
    private Task? _writer;
    private PcapNgWriter? _pcap;
    private long _written;
    private long _dropped;

    public bool IsActive { get; private set; }
    public string? CurrentPath { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public long FramesWritten => Interlocked.Read(ref _written);

    /// <summary>Frames discarded because the queue was full; a non-zero value means the file has gaps.</summary>
    public long FramesDropped => Interlocked.Read(ref _dropped);

    public event EventHandler? StateChanged;

    /// <summary>Same stem as the NDJSON log so a session's two files sit side by side.</summary>
    public static string BuildDefaultPath(string logsDir, DateTime startedLocal) =>
        Path.Combine(logsDir, startedLocal.ToString("yyyy-MM-dd_HH-mm-ss") + ".pcapng");

    public void Start(string path, LinkLayers linkLayer,
        string? nicDescription = null, string? captureFilter = null)
    {
        lock (_gate)
        {
            if (IsActive) throw new InvalidOperationException("Recorder already active");

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            _pcap = new PcapNgWriter(path, linkLayer,
                interfaceDescription: nicDescription,
                captureFilter: captureFilter,
                application: Application);
            _queue = new BlockingCollection<(byte[], DateTime)>(QueueCapacity);
            _written = 0;
            _dropped = 0;
            CurrentPath = path;
            StartedAtUtc = DateTime.UtcNow;
            IsActive = true;

            var queue = _queue;
            var pcap = _pcap;
            _writer = Task.Run(() => DrainLoop(queue, pcap));
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Queues a frame. A no-op when no session is recording.</summary>
    public void Append(byte[] frame, DateTime timestampUtc)
    {
        var q = _queue;
        if (q is null || q.IsAddingCompleted) return;
        try
        {
            if (!q.TryAdd((frame, timestampUtc))) Interlocked.Increment(ref _dropped);
        }
        catch (InvalidOperationException) { /* completed concurrently */ }
    }

    public void Stop()
    {
        BlockingCollection<(byte[], DateTime)>? queue;
        Task? writer;
        PcapNgWriter? pcap;
        lock (_gate)
        {
            if (!IsActive) return;
            queue = _queue;
            writer = _writer;
            pcap = _pcap;
            _queue = null;
            _writer = null;
            _pcap = null;
            IsActive = false;
        }

        // Drain in order: stop accepting, let the writer finish what is queued,
        // then close the file. Disposing early would cut the last block short and
        // leave exactly the truncated tail the reader has to tolerate.
        try { queue?.CompleteAdding(); } catch { /* already completed */ }
        try { writer?.Wait(); } catch { /* swallow */ }
        try { pcap?.Dispose(); } catch { /* swallow */ }
        try { queue?.Dispose(); } catch { /* swallow */ }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => Stop();

    private void DrainLoop(BlockingCollection<(byte[] Frame, DateTime Timestamp)> q, PcapNgWriter pcap)
    {
        try
        {
            foreach (var (frame, timestamp) in q.GetConsumingEnumerable())
            {
                pcap.Write(frame, timestamp);
                Interlocked.Increment(ref _written);
            }
        }
        catch (Exception)
        {
            // Best-effort, like the packet logger: a recording failure must not
            // take the app down mid-capture.
        }
    }
}
