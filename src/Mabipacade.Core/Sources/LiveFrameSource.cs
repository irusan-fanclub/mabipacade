using SharpPcap;

namespace Mabipacade.Core.Sources;

public sealed class LiveFrameSource : IFrameSource
{
    private readonly ICaptureDevice _device;
    private readonly string _bpfFilter;
    private Task? _stopTask;

    public LiveFrameSource(ICaptureDevice device, string bpfFilter)
    {
        _device = device;
        _bpfFilter = bpfFilter;
    }

    public event EventHandler<RawFrameEventArgs>? FrameReceived;
    public event EventHandler? EndOfStream;

    public Task StartAsync(CancellationToken ct)
    {
        _device.Open(DeviceModes.Promiscuous, 1000);
        _device.Filter = _bpfFilter;
        _device.OnPacketArrival += OnPacket;
        _device.StartCapture();
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _device.StopCapture();
        EndOfStream?.Invoke(this, EventArgs.Empty);
        _stopTask = Task.CompletedTask;
        return Task.CompletedTask;
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
        try { _stopTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _device.Close();
    }
}
