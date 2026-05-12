using System.Text.Json;
using Mabipacade.Cli.Recording;
using Mabipacade.Core.Diagnostics;
using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Sources;
using Mabipacade.Decoders;
using PacketDotNet;

namespace Mabipacade.Cli.Tests.Recording;

public class SessionRecorderTests
{
    [Fact]
    public void CreatesThreeFiles_InSessionDirectory()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"mp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            using var source = new TestFrameSource();
            var registry = new DecoderRegistry();
            DefaultDecoders.RegisterAll(registry);
            var pipeline = new PacketPipeline(source, registry);
            var recorder = new SessionRecorder(temp, region: "tw", processId: null,
                source: source, pipeline: pipeline, linkLayer: LinkLayers.Ethernet);

            recorder.Start();
            recorder.Stop("UserStop");

            Assert.True(File.Exists(Path.Combine(temp, "session.pcap")));
            Assert.True(File.Exists(Path.Combine(temp, "session.events.ndjson")));
            Assert.True(File.Exists(Path.Combine(temp, "session.json")));

            var meta = File.ReadAllText(Path.Combine(temp, "session.json"));
            Assert.Contains("\"region\": \"tw\"", meta);
            Assert.Contains("\"endedAt\":", meta);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    [Fact]
    public void SessionEvents_LandInEventsFile()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"mp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            using var source = new TestFrameSource();
            var pipeline = new PacketPipeline(source, new DecoderRegistry());
            var recorder = new SessionRecorder(temp, region: "tw", processId: null,
                source: source, pipeline: pipeline, linkLayer: LinkLayers.Ethernet);
            recorder.Start();

            recorder.WriteEvent(new SessionEvent.ConnectionLost(DateTime.UtcNow,
                new System.Net.IPEndPoint(System.Net.IPAddress.Parse("1.2.3.4"), 11000)));

            recorder.Stop("EOS");

            var events = File.ReadAllText(Path.Combine(temp, "session.events.ndjson"));
            Assert.Contains("\"type\":\"ConnectionLost\"", events);
            Assert.Contains("\"type\":\"SessionStart\"", events);
            Assert.Contains("\"type\":\"SessionEnd\"", events);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }
}

internal sealed class TestFrameSource : IFrameSource
{
    public event EventHandler<RawFrameEventArgs>? FrameReceived;
    public event EventHandler? EndOfStream;
    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
    public Task StopAsync() => Task.CompletedTask;
    public void Dispose() { }
    public void EmitFrame(byte[] data, DateTime ts) =>
        FrameReceived?.Invoke(this, new RawFrameEventArgs(data, LinkLayers.Ethernet, ts));
    public void EmitEos() => EndOfStream?.Invoke(this, EventArgs.Empty);
}
