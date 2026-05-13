using Mabipacade.Core.Diagnostics;
using Mabipacade.Core.Model;
using Mabipacade.Core.Pipeline;

namespace Mabipacade.DebugUi.Services;

public sealed class PipelineHost : IDisposable
{
    private readonly PacketPipeline _pipeline;
    private readonly IUiDispatcher _dispatcher;

    public event EventHandler<MabiPacket>? PacketReceived;
    public event EventHandler<SessionEvent>? SessionEventReceived;

    public PipelineHost(PacketPipeline pipeline, IUiDispatcher dispatcher)
    {
        _pipeline = pipeline;
        _dispatcher = dispatcher;
        _pipeline.PacketReceived += OnPacket;
        _pipeline.SessionEventReceived += OnEvent;
    }

    public PipelineMetrics Metrics => _pipeline.Metrics;

    public Task StartAsync(CancellationToken ct) => _pipeline.StartAsync(ct);
    public Task StopAsync() => _pipeline.StopAsync();

    internal void RaiseSessionEventForTests(SessionEvent ev) =>
        _dispatcher.BeginInvoke(() => SessionEventReceived?.Invoke(this, ev));

    private void OnPacket(object? sender, MabiPacket p) =>
        _dispatcher.BeginInvoke(() => PacketReceived?.Invoke(this, p));

    private void OnEvent(object? sender, SessionEvent ev) =>
        _dispatcher.BeginInvoke(() => SessionEventReceived?.Invoke(this, ev));

    public void Dispose()
    {
        _pipeline.PacketReceived -= OnPacket;
        _pipeline.SessionEventReceived -= OnEvent;
        _pipeline.Dispose();
    }
}
