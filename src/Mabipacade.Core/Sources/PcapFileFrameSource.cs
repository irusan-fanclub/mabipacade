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

    public PcapFileFrameSource(string path) { _path = path; }

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
        if (status == CaptureStoppedEventStatus.ErrorWhileCapturing)
        {
            _completion?.TrySetException(
                new InvalidOperationException("Pcap capture failed: " + status));
        }
        else
        {
            EndOfStream?.Invoke(this, EventArgs.Empty);
            _completion?.TrySetResult();
        }
    }

    private void OnPacket(object? sender, PacketCapture e)
    {
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
