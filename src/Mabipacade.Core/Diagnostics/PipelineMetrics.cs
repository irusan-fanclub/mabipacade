namespace Mabipacade.Core.Diagnostics;

public sealed class PipelineMetrics
{
    private long _frames, _packets, _badBody, _resync, _malformedFrames;

    public long TotalFrames => Interlocked.Read(ref _frames);
    public long TotalPackets => Interlocked.Read(ref _packets);
    public long BadBodyCount => Interlocked.Read(ref _badBody);
    public long FrameResyncCount => Interlocked.Read(ref _resync);

    /// <summary>
    /// Link-layer frames that could not be parsed at all — typically a capture
    /// taken with a small snaplen, which cuts frames mid-header.
    /// </summary>
    public long MalformedFrameCount => Interlocked.Read(ref _malformedFrames);

    public void IncrementFrames()   => Interlocked.Increment(ref _frames);
    public void IncrementPackets()  => Interlocked.Increment(ref _packets);
    public void IncrementBadBody()  => Interlocked.Increment(ref _badBody);
    public void IncrementResync()   => Interlocked.Increment(ref _resync);
    public void IncrementMalformedFrame() => Interlocked.Increment(ref _malformedFrames);
}
