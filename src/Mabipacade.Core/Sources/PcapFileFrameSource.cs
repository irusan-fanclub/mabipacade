using SharpPcap;
using SharpPcap.LibPcap;

namespace Mabipacade.Core.Sources;

public sealed class PcapFileFrameSource : IFrameSource
{
    private readonly string _path;
    private CaptureFileReaderDevice? _reader;
    private CancellationTokenSource? _cts;
    private TaskCompletionSource? _completion;
    private Task? _captureTask;
    private long _framesRead;

    public PcapFileFrameSource(string path) { _path = path; }

    /// <summary>
    /// True when the file ended mid-record — the recorder was killed before its
    /// buffered write completed. Frames read before that point are still valid.
    /// </summary>
    public bool Truncated { get; private set; }

    public event EventHandler<RawFrameEventArgs>? FrameReceived;
    public event EventHandler? EndOfStream;

    public Task StartAsync(CancellationToken ct)
    {
        _reader = new CaptureFileReaderDevice(_path);
        _reader.Open(new DeviceConfiguration());
        _reader.OnPacketArrival += OnPacket;
        _reader.OnCaptureStopped += OnCaptureStopped;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _completion = new TaskCompletionSource();
        _captureTask = Task.Run(() => _reader.Capture(), _cts.Token);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _reader?.StopCapture();
        return _completion?.Task ?? Task.CompletedTask;
    }

    private void OnCaptureStopped(object? sender, CaptureStoppedEventStatus status)
    {
        // libpcap reports a record it cannot finish reading as a capture error.
        // Once frames have come through, that means the file was cut mid-record
        // — the everyday result of killing a recorder — and the frames already
        // read are intact, so the read ends like any other end-of-file. Having
        // read nothing is a different claim: the file is unusable, and going
        // quiet would pass it off as an empty capture. Open() failures never
        // reach here; they throw from StartAsync.
        if (status == CaptureStoppedEventStatus.ErrorWhileCapturing)
        {
            if (Interlocked.Read(ref _framesRead) == 0)
            {
                _completion?.TrySetException(
                    new InvalidOperationException("Pcap capture failed: " + status));
                return;
            }
            Truncated = true;
        }

        EndOfStream?.Invoke(this, EventArgs.Empty);
        _completion?.TrySetResult();
    }

    private void OnPacket(object? sender, PacketCapture e)
    {
        Interlocked.Increment(ref _framesRead);
        var raw = e.GetPacket();
        FrameReceived?.Invoke(this, new RawFrameEventArgs(
            raw.Data,
            raw.LinkLayerType,
            raw.Timeval.Date.ToUniversalTime()));
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try { _captureTask?.Wait(TimeSpan.FromSeconds(5)); } catch { }
        _reader?.Close();
        _reader?.Dispose();
        _reader = null;
        _captureTask = null;
        _cts?.Dispose();
    }
}
