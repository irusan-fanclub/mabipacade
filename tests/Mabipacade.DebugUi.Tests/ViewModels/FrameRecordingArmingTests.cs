using System.IO;
using Mabipacade.Core.Capture;
using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Sources;
using Mabipacade.DebugUi.Services;
using Mabipacade.DebugUi.Tests.Fakes;
using Mabipacade.DebugUi.ViewModels;
using PacketDotNet;

namespace Mabipacade.DebugUi.Tests.ViewModels;

/// <summary>
/// Frame recording has to end up running whenever a live capture and a log are
/// both going, whichever order the two were started in. Getting this wrong is
/// silent: the NDJSON fills up normally and only the pcapng is missing.
/// </summary>
public class FrameRecordingArmingTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), $"mabipacade-arm-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    private sealed class IdleSource : IFrameSource
    {
        public event EventHandler<RawFrameEventArgs>? FrameReceived;
        public event EventHandler? EndOfStream;
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
        public void Dispose() { }
        public void Emit() => FrameReceived?.Invoke(this,
            new RawFrameEventArgs(new byte[] { 1, 2, 3, 4 }, LinkLayers.Ethernet, DateTime.UnixEpoch));
    }

    private sealed class EmptyTable : ITcpConnectionTable
    {
        public IReadOnlyList<TcpConnectionRow> GetConnections() => Array.Empty<TcpConnectionRow>();
    }

    private static LiveSession FakeLive(IdleSource source)
    {
        var pipeline = new PacketPipeline(source, new DecoderRegistry());
        var host = new PipelineHost(pipeline, new ImmediateDispatcher());
        var endpoint = new GameEndpoint(4812,
            System.Net.IPAddress.Parse("210.208.80.41"), 11022,
            System.Net.IPAddress.Parse("192.168.1.50"), 55588);
        var watchdog = new CaptureSession(new EmptyTable(), 4812, null);
        return new LiveSession(host, source, endpoint, "Test NIC", "tcp and (src net 210.208.80.0/24)", watchdog);
    }

    private MainViewModel NewVm() =>
        new(new ImmediateDispatcher()) { LogsDirectory = _dir };

    [Fact]
    public void LiveFirst_ThenLog_RecordsFrames()
    {
        var vm = NewVm();
        var source = new IdleSource();
        vm.BeginLiveSession(FakeLive(source));

        var logPath = vm.StartLogging();
        source.Emit();
        vm.StopLogging();

        Assert.True(File.Exists(Path.ChangeExtension(logPath, ".pcapng")));
    }

    [Fact]
    public void LogFirst_ThenLive_AlsoRecordsFrames()
    {
        // The order that produced .jsonl files with no capture beside them:
        // recording was armed once, at a moment when no live session existed
        // yet, and never reconsidered.
        var vm = NewVm();
        var source = new IdleSource();

        var logPath = vm.StartLogging();
        vm.BeginLiveSession(FakeLive(source));
        source.Emit();
        vm.StopLogging();

        Assert.True(File.Exists(Path.ChangeExtension(logPath, ".pcapng")));
    }

    [Fact]
    public void RestartingLiveWhileLogging_KeepsRecording()
    {
        // StartLive stops the previous session first, which disarms the
        // recorder; the replacement session has to re-arm it.
        var vm = NewVm();
        vm.BeginLiveSession(FakeLive(new IdleSource()));
        var logPath = vm.StartLogging();

        vm.StopActive();
        var second = new IdleSource();
        vm.BeginLiveSession(FakeLive(second));
        second.Emit();

        Assert.True(vm.CurrentCapturePath is not null,
            "frame recording should be running again after the new live session");
        vm.StopLogging();
        Assert.True(File.Exists(Path.ChangeExtension(logPath, ".pcapng")));
    }

    [Fact]
    public void Replay_DoesNotRecordFrames()
    {
        // No live session: the frames would come from a capture file that
        // already exists.
        var vm = NewVm();
        var logPath = vm.StartLogging();
        vm.StopLogging();

        Assert.False(File.Exists(Path.ChangeExtension(logPath, ".pcapng")));
    }
}
