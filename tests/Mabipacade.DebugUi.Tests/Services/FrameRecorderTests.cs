using System.IO;
using Mabipacade.Core.Sources;
using Mabipacade.DebugUi.Services;
using PacketDotNet;

namespace Mabipacade.DebugUi.Tests.Services;

public class FrameRecorderTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), $"mabipacade-rec-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    private string InDir(string name) => Path.Combine(_dir, name);

    private static async Task<List<byte[]>> ReadFramesAsync(string path)
    {
        var frames = new List<byte[]>();
        using var src = new PcapFileFrameSource(path);
        src.FrameReceived += (_, e) => { lock (frames) frames.Add(e.Data); };
        await src.StartAsync(CancellationToken.None);
        await src.StopAsync();
        return frames;
    }

    [Fact]
    public async Task RecordedFrames_ReadBackFromTheFile()
    {
        var path = InDir("session.pcapng");
        using var rec = new FrameRecorder();
        rec.Start(path, LinkLayers.Ethernet);
        rec.Append(new byte[] { 1, 2, 3 }, DateTime.UnixEpoch);
        rec.Append(new byte[] { 4, 5, 6, 7 }, DateTime.UnixEpoch.AddSeconds(1));
        rec.Stop();

        var frames = await ReadFramesAsync(path);
        Assert.Equal(2, frames.Count);
        Assert.Equal(new byte[] { 1, 2, 3 }, frames[0]);
        Assert.Equal(new byte[] { 4, 5, 6, 7 }, frames[1]);
    }

    [Fact]
    public void State_TracksStartAndStop()
    {
        using var rec = new FrameRecorder();
        int stateChanges = 0;
        rec.StateChanged += (_, _) => Interlocked.Increment(ref stateChanges);

        Assert.False(rec.IsActive);
        rec.Start(InDir("a.pcapng"), LinkLayers.Ethernet);
        Assert.True(rec.IsActive);
        Assert.Equal(InDir("a.pcapng"), rec.CurrentPath);

        rec.Stop();
        Assert.False(rec.IsActive);
        Assert.Equal(2, stateChanges);
    }

    [Fact]
    public void Stop_IsIdempotent()
    {
        using var rec = new FrameRecorder();
        rec.Start(InDir("a.pcapng"), LinkLayers.Ethernet);
        rec.Stop();
        rec.Stop();   // must not throw or double-close the file
        Assert.False(rec.IsActive);
    }

    [Fact]
    public void Append_BeforeStart_IsDropped()
    {
        // Frames keep arriving from the capture whether or not recording is on,
        // so an append outside a session has to be a no-op rather than a fault.
        using var rec = new FrameRecorder();
        rec.Append(new byte[] { 1 }, DateTime.UnixEpoch);
        Assert.Equal(0, rec.FramesWritten);
    }

    [Fact]
    public async Task Stop_FlushesEveryQueuedFrame()
    {
        // Writing happens on a background thread; stopping must drain it, or the
        // tail of a session silently goes missing.
        var path = InDir("many.pcapng");
        using var rec = new FrameRecorder();
        rec.Start(path, LinkLayers.Ethernet);
        for (int i = 0; i < 500; i++)
            rec.Append(new byte[] { (byte)i, 0xAA }, DateTime.UnixEpoch.AddMilliseconds(i));
        rec.Stop();

        Assert.Equal(500, rec.FramesWritten);
        Assert.Equal(500, (await ReadFramesAsync(path)).Count);
    }

    [Fact]
    public void BuildDefaultPath_MatchesTheLogNamingScheme()
    {
        // Same stem as PacketLogger's .jsonl so a session's two files pair up.
        var path = FrameRecorder.BuildDefaultPath(_dir, new DateTime(2026, 8, 9, 14, 30, 0));
        Assert.Equal(InDir("2026-08-09_14-30-00.pcapng"), path);
    }

    [Fact]
    public void Start_WhileActive_Throws()
    {
        using var rec = new FrameRecorder();
        rec.Start(InDir("a.pcapng"), LinkLayers.Ethernet);
        Assert.Throws<InvalidOperationException>(() => rec.Start(InDir("b.pcapng"), LinkLayers.Ethernet));
    }
}
