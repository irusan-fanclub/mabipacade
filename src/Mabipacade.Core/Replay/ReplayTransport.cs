using Mabipacade.Core.Sources;

namespace Mabipacade.Core.Replay;

public enum ReplayState { Stopped, Playing, Paused }

public sealed class ReplayTransport : IFrameSource
{
    private readonly IFrameSource _source;
    private readonly ManualResetEventSlim _playGate = new(initialState: false);
    private TaskCompletionSource? _completion;
    private CancellationTokenSource? _cts;
    private int _stepRemaining;
    private DateTime _lastFrameTs;
    private bool _isFirstFrame = true;

    public ReplayState State { get; private set; } = ReplayState.Stopped;
    public TimeSpan Position { get; private set; } = TimeSpan.Zero;
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    public double Rate { get; set; } = 1.0;

    public event EventHandler<RawFrameEventArgs>? FrameReceived;
    public event EventHandler? EndOfStream;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<ReplayState>? StateChanged;

    public ReplayTransport(IFrameSource source)
    {
        _source = source;
        _source.FrameReceived += OnFrame;
        _source.EndOfStream += OnEos;
    }

    public Task StartAsync(CancellationToken ct)
    {
        Play();
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        Stop();
        return Task.CompletedTask;
    }

    public void Play()
    {
        var wasStopped = State == ReplayState.Stopped;
        State = ReplayState.Playing;
        _playGate.Set();
        StateChanged?.Invoke(this, State);
        if (wasStopped)
        {
            _cts = new CancellationTokenSource();
            _completion = new TaskCompletionSource();
            _isFirstFrame = true;
            _ = _source.StartAsync(_cts.Token);
        }
    }

    public void Pause()
    {
        if (State != ReplayState.Playing) return;
        _playGate.Reset();
        State = ReplayState.Paused;
        StateChanged?.Invoke(this, State);
    }

    public void Stop()
    {
        if (State == ReplayState.Stopped) return;
        State = ReplayState.Stopped;
        _playGate.Set();
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
        if (State == ReplayState.Stopped) return;

        try { _playGate.Wait(_cts?.Token ?? CancellationToken.None); }
        catch (OperationCanceledException) { return; }

        if (State == ReplayState.Stopped) return;

        if (!_isFirstFrame && Rate > 0)
        {
            var pcapDelta = e.TimestampUtc - _lastFrameTs;
            if (pcapDelta > TimeSpan.Zero)
            {
                var wallDelta = TimeSpan.FromTicks((long)(pcapDelta.Ticks / Rate));
                if (wallDelta >= TimeSpan.FromMilliseconds(1))
                {
                    try { Task.Delay(wallDelta, _cts!.Token).Wait(); }
                    catch (AggregateException ex) when (ex.InnerException is OperationCanceledException) { return; }
                }
            }
        }
        _lastFrameTs = e.TimestampUtc;
        _isFirstFrame = false;

        FrameReceived?.Invoke(this, e);
        Position = e.TimestampUtc - DateTime.UnixEpoch;
        PositionChanged?.Invoke(this, Position);

        if (_stepRemaining > 0 && --_stepRemaining == 0) Pause();
    }

    private void OnEos(object? sender, EventArgs e)
    {
        State = ReplayState.Stopped;
        _playGate.Set();
        StateChanged?.Invoke(this, State);
        EndOfStream?.Invoke(this, EventArgs.Empty);
        _completion?.TrySetResult();
    }

    public void Dispose()
    {
        _source.FrameReceived -= OnFrame;
        _source.EndOfStream -= OnEos;
        _cts?.Cancel();
        _cts?.Dispose();
        _playGate.Dispose();
        _source.Dispose();
    }
}
