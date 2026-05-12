using Mabipacade.Core.Sources;

namespace Mabipacade.Core.Replay;

public enum ReplayState { Stopped, Playing, Paused }

public sealed class ReplayTransport : IDisposable
{
    private readonly IFrameSource _source;
    private TaskCompletionSource? _completion;
    private CancellationTokenSource? _cts;
    private int _stepRemaining;

    public ReplayTransport(IFrameSource source)
    {
        _source = source;
        _source.FrameReceived += OnFrame;
        _source.EndOfStream += OnEos;
    }

    public ReplayState State { get; private set; } = ReplayState.Stopped;
    public TimeSpan Position { get; private set; } = TimeSpan.Zero;
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    public double Rate { get; set; } = 1.0;

    public event EventHandler<RawFrameEventArgs>? FrameEmitted;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<ReplayState>? StateChanged;

    public void Play()
    {
        if (State == ReplayState.Playing) return;
        State = ReplayState.Playing;
        StateChanged?.Invoke(this, State);
        _cts = new CancellationTokenSource();
        _completion = new TaskCompletionSource();
        _ = _source.StartAsync(_cts.Token);
    }

    public void Pause()
    {
        if (State == ReplayState.Playing) { State = ReplayState.Paused; StateChanged?.Invoke(this, State); }
    }

    public void Stop()
    {
        State = ReplayState.Stopped;
        _cts?.Cancel();
        _source.StopAsync();
        StateChanged?.Invoke(this, State);
        _completion?.TrySetResult();
    }

    public void StepForward(int n = 1)
    {
        if (n < 1) return;
        Pause();
        _stepRemaining = n;
        Play();
    }

    public void SeekForwardTo(TimeSpan t)
    {
        if (t < Position) throw new ArgumentException("Cannot seek backward; reload pcap instead.");
        Position = t;
        PositionChanged?.Invoke(this, Position);
    }

    public Task WaitForCompletionAsync() => _completion?.Task ?? Task.CompletedTask;

    private void OnFrame(object? sender, RawFrameEventArgs e)
    {
        if (State != ReplayState.Playing) return;
        FrameEmitted?.Invoke(this, e);
        Position = e.TimestampUtc - DateTime.UnixEpoch;
        PositionChanged?.Invoke(this, Position);
        if (_stepRemaining > 0 && --_stepRemaining == 0) Pause();
    }

    private void OnEos(object? sender, EventArgs e)
    {
        State = ReplayState.Stopped;
        StateChanged?.Invoke(this, State);
        _completion?.TrySetResult();
    }

    public void Dispose()
    {
        _source.FrameReceived -= OnFrame;
        _source.EndOfStream -= OnEos;
        _cts?.Dispose();
    }
}
