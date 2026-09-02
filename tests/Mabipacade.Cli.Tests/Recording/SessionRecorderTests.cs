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
                source: source, pipeline: pipeline, linkLayer: LinkLayers.Ethernet,
                nicDescription: "Test NIC", captureFilter: "tcp and src host 1.2.3.4");

            recorder.Start();
            source.EmitFrame(new byte[] { 1, 2, 3, 4 }, DateTime.UnixEpoch);
            recorder.Stop("UserStop");

            Assert.True(File.Exists(Path.Combine(temp, "session.pcapng")));
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
    public void RecordedCapture_CarriesTheNicAndFilter()
    {
        // pcapng keeps this in the interface block, so a capture handed to
        // someone else says what NIC and BPF filter produced it — metadata
        // classic pcap had nowhere to put.
        var temp = Path.Combine(Path.GetTempPath(), $"mp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            using var source = new TestFrameSource();
            var pipeline = new PacketPipeline(source, new DecoderRegistry());
            var recorder = new SessionRecorder(temp, region: "tw", processId: null,
                source: source, pipeline: pipeline, linkLayer: LinkLayers.Ethernet,
                nicDescription: "Realtek Gaming 2.5GbE", captureFilter: "tcp and src port 11022");
            recorder.Start();
            recorder.Stop("UserStop");

            var bytes = File.ReadAllBytes(Path.Combine(temp, "session.pcapng"));
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            Assert.Contains("Realtek Gaming 2.5GbE", text);
            Assert.Contains("tcp and src port 11022", text);
            Assert.Contains("mabipacade", text);
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
