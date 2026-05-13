using Mabipacade.Core.Diagnostics;
using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Sources;
using Mabipacade.DebugUi.Services;
using Mabipacade.DebugUi.Tests.Fakes;
using PacketDotNet;

namespace Mabipacade.DebugUi.Tests.Services;

public class PipelineHostTests
{
    private sealed class TestSource : IFrameSource
    {
#pragma warning disable CS0067
        public event EventHandler<RawFrameEventArgs>? FrameReceived;
        public event EventHandler? EndOfStream;
#pragma warning restore CS0067
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
        public void Dispose() { }
    }

    [Fact]
    public void Construct_HoldsMetrics()
    {
        var source = new TestSource();
        var pipeline = new PacketPipeline(source, new DecoderRegistry());
        using var host = new PipelineHost(pipeline, new ImmediateDispatcher());
        Assert.NotNull(host.Metrics);
    }

    [Fact]
    public void Forwards_SessionEvent_ToHandler()
    {
        var source = new TestSource();
        var pipeline = new PacketPipeline(source, new DecoderRegistry());
        using var host = new PipelineHost(pipeline, new ImmediateDispatcher());

        SessionEvent? captured = null;
        host.SessionEventReceived += (_, e) => captured = e;

        host.RaiseSessionEventForTests(new SessionEvent.SessionStart(DateTime.UtcNow, "tw", null));

        Assert.NotNull(captured);
        Assert.IsType<SessionEvent.SessionStart>(captured);
    }
}
