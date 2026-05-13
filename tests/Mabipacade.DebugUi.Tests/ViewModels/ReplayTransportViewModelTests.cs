using Mabipacade.Core.Replay;
using Mabipacade.Core.Sources;
using Mabipacade.DebugUi.ViewModels;

namespace Mabipacade.DebugUi.Tests.ViewModels;

public class ReplayTransportViewModelTests
{
    private sealed class FakeSource : IFrameSource
    {
        public event EventHandler<RawFrameEventArgs>? FrameReceived;
        public event EventHandler? EndOfStream;
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
        public void Dispose() { }
        public void Eos() => EndOfStream?.Invoke(this, EventArgs.Empty);
    }

    [Fact]
    public void Initial_StateIsStopped()
    {
        var transport = new ReplayTransport(new FakeSource());
        var vm = new ReplayTransportViewModel(transport);
        Assert.Equal(ReplayState.Stopped, vm.State);
        Assert.Equal(1.0, vm.Rate);
    }

    [Fact]
    public void Play_UpdatesState_AndFiresPropertyChanged()
    {
        var transport = new ReplayTransport(new FakeSource());
        var vm = new ReplayTransportViewModel(transport);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.PlayCommand.Execute(null);

        Assert.Equal(ReplayState.Playing, vm.State);
        Assert.Contains(nameof(ReplayTransportViewModel.State), changed);
    }

    [Fact]
    public void Rate_PropagatesToTransport()
    {
        var transport = new ReplayTransport(new FakeSource());
        var vm = new ReplayTransportViewModel(transport);
        vm.Rate = 4.0;
        Assert.Equal(4.0, transport.Rate);
    }
}
