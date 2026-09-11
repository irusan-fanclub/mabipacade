using Mabipacade.Core.Sources;
using Mabipacade.Core.Tests.Pipeline;
using PacketDotNet;

namespace Mabipacade.Core.Tests.Sources;

public class ClientTrafficFilterSourceTests
{
    private const ushort ClientPort = 55588;
    private const ushort VmNatPort = 61000;
    private const ushort ServerPort = 11022;

    private sealed class ControllableSource : IFrameSource
    {
        public event EventHandler<RawFrameEventArgs>? FrameReceived;
        public event EventHandler? EndOfStream;
        public bool Started { get; private set; }
        public bool Stopped { get; private set; }
        public bool Disposed { get; private set; }

        public Task StartAsync(CancellationToken ct) { Started = true; return Task.CompletedTask; }
        public Task StopAsync() { Stopped = true; return Task.CompletedTask; }
        public void Dispose() => Disposed = true;

        public void Emit(byte[] frame) =>
            FrameReceived?.Invoke(this, new RawFrameEventArgs(frame, LinkLayers.Ethernet, DateTime.UnixEpoch));

        public void EmitEos() => EndOfStream?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>An inbound frame: game server to a local port.</summary>
    private static byte[] Inbound(ushort localPort, string serverIp = "210.208.80.41") =>
        TestEthernetBuilder.WrapTcp(new byte[] { 1, 2, 3, 4 },
            srcPort: ServerPort, dstPort: localPort,
            srcIp: serverIp, dstIp: "192.168.1.50");

    private static (ControllableSource Inner, List<byte[]> Passed, List<ushort> Vetted)
        Build(params ushort[] clientPorts)
    {
        var inner = new ControllableSource();
        var passed = new List<byte[]>();
        var vetted = new List<ushort>();
        var filter = new ClientTrafficFilterSource(inner, port =>
        {
            vetted.Add(port);
            return clientPorts.Contains(port);
        });
        filter.FrameReceived += (_, e) => passed.Add(e.Data);
        return (inner, passed, vetted);
    }

    [Fact]
    public void PassesFramesAddressedToTheClientsPort()
    {
        var (inner, passed, _) = Build(ClientPort);
        inner.Emit(Inbound(ClientPort));
        Assert.Single(passed);
    }

    [Fact]
    public void DropsFramesAddressedToAnotherProcessPort()
    {
        // The reported case: a NAT'd VM shares our address, so only the local
        // port distinguishes its traffic from ours.
        var (inner, passed, _) = Build(ClientPort);
        inner.Emit(Inbound(VmNatPort));
        Assert.Empty(passed);
    }

    [Fact]
    public void DecidesOncePerStream()
    {
        // The verdict is cached per stream, so a busy connection does not vet
        // every frame — this is what keeps the TCP table off the hot path.
        var (inner, passed, vetted) = Build(ClientPort);

        for (int i = 0; i < 20; i++) inner.Emit(Inbound(ClientPort));
        for (int i = 0; i < 20; i++) inner.Emit(Inbound(VmNatPort));

        Assert.Equal(20, passed.Count);
        Assert.Equal(new ushort[] { ClientPort, VmNatPort }, vetted);
    }

    [Fact]
    public void TreatsDifferentServersAsDifferentStreams()
    {
        // A channel switch moves to another address in the same network; that is
        // a new stream and gets its own verdict.
        var (inner, passed, vetted) = Build(ClientPort);

        inner.Emit(Inbound(ClientPort, serverIp: "210.208.80.41"));
        inner.Emit(Inbound(ClientPort, serverIp: "210.208.80.34"));

        Assert.Equal(2, passed.Count);
        Assert.Equal(2, vetted.Count);
    }

    /// <summary>An outbound frame: the same stream as <see cref="Inbound"/>, mirrored.</summary>
    private static byte[] Outbound(ushort localPort, string serverIp = "210.208.80.41") =>
        TestEthernetBuilder.WrapTcp(new byte[] { 1, 2, 3, 4 },
            srcPort: localPort, dstPort: ServerPort,
            srcIp: "192.168.1.50", dstIp: serverIp);

    [Fact]
    public void BothDirectionsOfAStream_ShareOneKeyAndOneVerdict()
    {
        var inner = new ControllableSource();
        var passed = new List<byte[]>();
        var vetted = new List<ushort>();
        var filter = new ClientTrafficFilterSource(inner,
            port => { vetted.Add(port); return port == ClientPort; },
            Mabipacade.Core.Pipeline.DirectionClassifiers.ByServerEndpoint(
                Mabipacade.Core.Capture.RegionProfiles.Taiwan));
        filter.FrameReceived += (_, e) => passed.Add(e.Data);

        inner.Emit(Inbound(ClientPort));
        inner.Emit(Outbound(ClientPort));   // mirrored key folds onto the same stream
        inner.Emit(Outbound(VmNatPort));    // the VM's own outbound is a foreign stream

        Assert.Equal(2, passed.Count);
        // One vet per stream: the outbound frame of our stream reused the verdict.
        Assert.Equal(new ushort[] { ClientPort, VmNatPort }, vetted);
    }

    [Fact]
    public void DropsFramesItCannotParse_WithoutThrowing()
    {
        // Foreign traffic includes whatever else is on the wire; a frame cut
        // mid-header must not escape into the capture callback.
        var (inner, passed, _) = Build(ClientPort);
        var truncated = Inbound(ClientPort).AsSpan(0, 20).ToArray();

        inner.Emit(truncated);
        Assert.Empty(passed);
    }

    [Fact]
    public void ForwardsEndOfStream()
    {
        var inner = new ControllableSource();
        using var filter = new ClientTrafficFilterSource(inner, _ => true);
        bool eos = false;
        filter.EndOfStream += (_, _) => eos = true;

        inner.EmitEos();
        Assert.True(eos);
    }

    [Fact]
    public async Task DelegatesLifecycleToTheInnerSource()
    {
        var inner = new ControllableSource();
        var filter = new ClientTrafficFilterSource(inner, _ => true);

        await filter.StartAsync(CancellationToken.None);
        Assert.True(inner.Started);

        await filter.StopAsync();
        Assert.True(inner.Stopped);

        filter.Dispose();
        Assert.True(inner.Disposed);
    }
}
