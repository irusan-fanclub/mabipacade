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
