using Mabipacade.Core.Model;
using Mabipacade.Core.Replay;

namespace Mabipacade.Core.Tests.Replay;

public class ReplayTransportTests
{
    [Fact]
    public void Initial_StateIsStopped()
    {
        var transport = new ReplayTransport(new FakeReplaySource());
        Assert.Equal(ReplayState.Stopped, transport.State);
    }

    [Fact]
    public async Task Play_TransitionsToPlaying_AndEmits()
    {
        var src = new FakeReplaySource(
            (DateTime.UnixEpoch.AddSeconds(0), new byte[] { 1 }),
            (DateTime.UnixEpoch.AddSeconds(0.001), new byte[] { 2 }),
            (DateTime.UnixEpoch.AddSeconds(0.002), new byte[] { 3 }));
        var transport = new ReplayTransport(src) { Rate = 100 };
        var received = new List<byte[]>();
        transport.FrameEmitted += (_, e) => received.Add(e.Data);

        transport.Play();
        await transport.WaitForCompletionAsync();

        Assert.Equal(ReplayState.Stopped, transport.State);
        Assert.Equal(3, received.Count);
    }

    [Fact]
    public void SeekForwardTo_Earlier_Throws()
    {
        var transport = new ReplayTransport(new FakeReplaySource());
        Assert.Throws<ArgumentException>(() => transport.SeekForwardTo(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public async Task Throttle_AtRateOne_SpacesFramesByPcapDelta()
    {
        var t0 = DateTime.UnixEpoch;
        var src = new FakeReplaySource(
            (t0, new byte[] { 1 }),
            (t0.AddMilliseconds(200), new byte[] { 2 }),
            (t0.AddMilliseconds(400), new byte[] { 3 }));
        var transport = new ReplayTransport(src) { Rate = 1.0 };

        var received = new List<DateTime>();
        transport.FrameEmitted += (_, e) => received.Add(DateTime.UtcNow);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        transport.Play();
        await transport.WaitForCompletionAsync();
        sw.Stop();

        Assert.Equal(3, received.Count);
        Assert.True(sw.ElapsedMilliseconds >= 350,
            $"Rate=1.0 should pace ≥ 350ms wall-clock for 400ms pcap span; got {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task Throttle_AtHighRate_IsFastButNotInstant()
    {
        var t0 = DateTime.UnixEpoch;
        var src = new FakeReplaySource(
            (t0, new byte[] { 1 }),
            (t0.AddMilliseconds(200), new byte[] { 2 }),
            (t0.AddMilliseconds(400), new byte[] { 3 }));
        var transport = new ReplayTransport(src) { Rate = 100.0 };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        transport.Play();
        await transport.WaitForCompletionAsync();
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 200,
            $"Rate=100 should compress 400ms pcap span to < 200ms wall-clock; got {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task Pause_BlocksFurtherFrames_ResumeContinues()
    {
        var t0 = DateTime.UnixEpoch;
        var src = new ManualReplaySource();
        var transport = new ReplayTransport(src) { Rate = 100.0 };

        int received = 0;
        transport.FrameEmitted += (_, _) => Interlocked.Increment(ref received);

        transport.Play();
        src.EmitFrame(t0, new byte[] { 1 });
        await Task.Delay(50);
        Assert.Equal(1, received);

        transport.Pause();
        src.EmitFrame(t0.AddMilliseconds(10), new byte[] { 2 });
        await Task.Delay(50);
        Assert.Equal(1, received);

        transport.Play();
        await Task.Delay(100);
        Assert.Equal(2, received);

        src.SignalEos();
        await transport.WaitForCompletionAsync();
    }
}

internal sealed class ManualReplaySource : Mabipacade.Core.Sources.IFrameSource
{
    public event EventHandler<Mabipacade.Core.Sources.RawFrameEventArgs>? FrameReceived;
    public event EventHandler? EndOfStream;
    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
    public Task StopAsync() => Task.CompletedTask;
    public void Dispose() { }
    public void EmitFrame(DateTime ts, byte[] data) =>
        Task.Run(() => FrameReceived?.Invoke(this, new Mabipacade.Core.Sources.RawFrameEventArgs(data, PacketDotNet.LinkLayers.Ethernet, ts)));
    public void SignalEos() => EndOfStream?.Invoke(this, EventArgs.Empty);
}

internal sealed class FakeReplaySource : Mabipacade.Core.Sources.IFrameSource
{
    private readonly (DateTime ts, byte[] data)[] _frames;
    public FakeReplaySource(params (DateTime ts, byte[] data)[] frames) { _frames = frames; }
    public event EventHandler<Mabipacade.Core.Sources.RawFrameEventArgs>? FrameReceived;
    public event EventHandler? EndOfStream;
    public Task StartAsync(CancellationToken ct)
    {
        foreach (var (ts, data) in _frames)
            FrameReceived?.Invoke(this, new Mabipacade.Core.Sources.RawFrameEventArgs(data, PacketDotNet.LinkLayers.Ethernet, ts));
        EndOfStream?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
    public Task StopAsync() => Task.CompletedTask;
    public void Dispose() { }
}
