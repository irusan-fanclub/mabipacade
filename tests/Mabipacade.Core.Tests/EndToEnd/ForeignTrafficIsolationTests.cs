using System.Net;
using Mabipacade.Core.Capture;
using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Sources;
using Mabipacade.Core.Tests.Pipeline;
using PacketDotNet;

namespace Mabipacade.Core.Tests.EndToEnd;

/// <summary>
/// The composition that answers the original complaint: a NAT'd virtual machine
/// running its own client shares this host's address, so its traffic reaches the
/// capture. Everything downstream — the recorder as much as the decoder — must
/// see only our own frames.
/// </summary>
public class ForeignTrafficIsolationTests
{
    private const int ClientPid = 4812;
    private const int VmNatPid = 7777;
    private const ushort ClientPort = 55588;
    private const ushort VmNatPort = 61000;
    private const string HostIp = "192.168.1.50";
    private const string ServerIp = "210.208.80.41";

    private sealed class FakeTable : ITcpConnectionTable
    {
        public List<TcpConnectionRow> Rows { get; } = new();
        public IReadOnlyList<TcpConnectionRow> GetConnections() => Rows;
    }

    private sealed class ScriptedSource : IFrameSource
    {
        private readonly byte[][] _frames;
        public ScriptedSource(params byte[][] frames) { _frames = frames; }
        public event EventHandler<RawFrameEventArgs>? FrameReceived;
        public event EventHandler? EndOfStream;
        public Task StartAsync(CancellationToken ct)
        {
            foreach (var f in _frames)
                FrameReceived?.Invoke(this, new RawFrameEventArgs(f, LinkLayers.Ethernet, DateTime.UnixEpoch));
            EndOfStream?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
        public Task StopAsync() => Task.CompletedTask;
        public void Dispose() { }
    }

    /// <summary>Both machines share the host address; only the local port differs.</summary>
    private static byte[] Frame(ushort localPort, byte[] payload) =>
        TestEthernetBuilder.WrapTcp(payload, srcPort: 11022, dstPort: localPort,
            srcIp: ServerIp, dstIp: HostIp);

    private static TcpConnectionRow Conn(ushort localPort, int pid) =>
        new(IPAddress.Parse(HostIp), localPort, IPAddress.Parse(ServerIp), 11022,
            TcpConnectionState.Established, pid);

    [Fact]
    public async Task OnlyTheClientsFramesReachTheRecorderAndTheDecoder()
    {
        var packet = TestPacketBuilder.BuildNormal(op: 0x00005209, entityId: 0UL,
            bodyTail: new byte[] { 0x00, 0x00, 0x00 });

        var table = new FakeTable();
        table.Rows.Add(Conn(ClientPort, ClientPid));
        table.Rows.Add(Conn(VmNatPort, VmNatPid));
        var tracker = new ClientConnectionTracker(table, ClientPid);

        var inner = new ScriptedSource(
            Frame(VmNatPort, packet),      // the VM's client, same address as ours
            Frame(ClientPort, packet),     // ours
            Frame(VmNatPort, packet));

        using var source = new ClientTrafficFilterSource(inner, tracker.IsClientLocalPort);

        // The recorder subscribes to the source, upstream of the pipeline —
        // which is exactly why the filter has to sit here and not in the
        // pipeline, or foreign frames would still land in the pcapng.
        var recorded = new List<byte[]>();
        source.FrameReceived += (_, e) => recorded.Add(e.Data);

        var pipeline = new PacketPipeline(source, new DecoderRegistry());
        var decoded = new List<Mabipacade.Core.Model.MabiPacket>();
        pipeline.PacketReceived += (_, p) => decoded.Add(p);

        await pipeline.StartAsync(CancellationToken.None);
        await pipeline.StopAsync();

        Assert.Single(recorded);
        Assert.Single(decoded);
        Assert.Equal((uint)0x00005209, decoded[0].Op);
    }

    [Fact]
    public async Task AChannelSwitchToANewLocalPort_IsPickedUpWithoutWaiting()
    {
        // The switch opens a socket between the watchdog's polls. The port vet
        // re-reads the table on a miss, so the first frame of the new connection
        // is admitted rather than dropped — and that first frame is where the
        // full-character snapshot arrives.
        const ushort SwitchedPort = 59599;
        var packet = TestPacketBuilder.BuildNormal(op: 0x00005209, entityId: 0UL,
            bodyTail: new byte[] { 0x00, 0x00, 0x00 });

        var table = new FakeTable();
        table.Rows.Add(Conn(ClientPort, ClientPid));
        var tracker = new ClientConnectionTracker(table, ClientPid);

        // Prime the cache on the old connection, then switch.
        Assert.True(tracker.IsClientLocalPort(ClientPort));
        table.Rows.Clear();
        table.Rows.Add(Conn(SwitchedPort, ClientPid));

        var inner = new ScriptedSource(
            // A new connection opens with its 4-byte key, then the snapshot.
            Frame(SwitchedPort, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }),
            Frame(SwitchedPort, packet));
        using var source = new ClientTrafficFilterSource(inner, tracker.IsClientLocalPort);

        var pipeline = new PacketPipeline(source, new DecoderRegistry());
        var decoded = new List<Mabipacade.Core.Model.MabiPacket>();
        pipeline.PacketReceived += (_, p) => decoded.Add(p);

        await pipeline.StartAsync(CancellationToken.None);
        await pipeline.StopAsync();

        Assert.Equal((uint)0x00005209, Assert.Single(decoded).Op);
        Assert.Equal(0, pipeline.Metrics.FrameResyncCount);
    }
}
