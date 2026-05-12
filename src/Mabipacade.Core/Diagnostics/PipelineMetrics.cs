namespace Mabipacade.Core.Diagnostics;

public sealed class PipelineMetrics
{
    private long _frames, _packets, _badBody, _resync;

    public long TotalFrames => Interlocked.Read(ref _frames);
    public long TotalPackets => Interlocked.Read(ref _packets);
    public long BadBodyCount => Interlocked.Read(ref _badBody);
    public long FrameResyncCount => Interlocked.Read(ref _resync);

    public void IncrementFrames()   => Interlocked.Increment(ref _frames);
    public void IncrementPackets()  => Interlocked.Increment(ref _packets);
    public void IncrementBadBody()  => Interlocked.Increment(ref _badBody);
    public void IncrementResync()   => Interlocked.Increment(ref _resync);
}
