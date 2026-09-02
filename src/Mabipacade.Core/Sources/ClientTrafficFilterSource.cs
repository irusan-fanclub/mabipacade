using System.Net;
using PacketDotNet;

namespace Mabipacade.Core.Sources;

/// <summary>
/// Passes on only the frames belonging to the game process, dropping everything
/// else the capture picks up.
///
/// The capture filter covers a whole server network so a channel switch is never
/// missed, and the device is opened in promiscuous mode, so frames from other
/// machines and processes reach us too. A NAT'd virtual machine running its own
/// client is the hard case: its packets carry the host's address and differ only
/// in the local port.
///
/// This sits between the capture and everything downstream — the recorder
/// included — so foreign traffic never reaches the pcapng, not merely never
/// reaches the decoder.
/// </summary>
public sealed class ClientTrafficFilterSource : IFrameSource
{
    /// <summary>
    /// A connection, identified from the server's side so both directions of
    /// the same TCP stream share one key — and therefore one verdict. The
    /// local address is left out: frames here either target this machine or
    /// were sent by it.
    /// </summary>
    private readonly record struct StreamKey(IPAddress ServerAddress, ushort ServerPort, ushort LocalPort);

    private readonly IFrameSource _inner;
    private readonly Func<ushort, bool> _isClientPort;
    private readonly Pipeline.DirectionClassifier _classify;

    /// <summary>
    /// Verdict per stream, decided at first sight and kept. Vetting a port means
    /// reading the OS TCP table, which must not happen per frame.
    /// </summary>
    private readonly Dictionary<StreamKey, bool> _verdicts = new();

    public ClientTrafficFilterSource(IFrameSource inner, Func<ushort, bool> isClientPort,
        Pipeline.DirectionClassifier? directionClassifier = null)
    {
        _inner = inner;
        _isClientPort = isClientPort;
        _classify = directionClassifier ?? Pipeline.DirectionClassifiers.InboundOnly;
        _inner.FrameReceived += OnFrame;
        _inner.EndOfStream += OnEndOfStream;
    }

    public event EventHandler<RawFrameEventArgs>? FrameReceived;
    public event EventHandler? EndOfStream;

    public Task StartAsync(CancellationToken ct) => _inner.StartAsync(ct);

    public Task StopAsync() => _inner.StopAsync();

    private void OnFrame(object? sender, RawFrameEventArgs e)
    {
        if (!TryReadStream(e, out var key)) return;

        if (!_verdicts.TryGetValue(key, out bool accepted))
        {
            accepted = _isClientPort(key.LocalPort);
            _verdicts[key] = accepted;
        }
        if (!accepted) return;

        FrameReceived?.Invoke(this, e);
    }

    private void OnEndOfStream(object? sender, EventArgs e) =>
        EndOfStream?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Reads the stream a frame belongs to. False for anything that is not
    /// parseable TCP — a frame cut mid-header by a small snaplen, or whatever
    /// else the promiscuous capture picked up — which is dropped rather than
    /// allowed to throw inside the capture callback.
    /// </summary>
    private bool TryReadStream(RawFrameEventArgs e, out StreamKey key)
    {
        try
        {
            var parsed = Packet.ParsePacket(e.LinkLayer, e.Data);
            var tcp = parsed.Extract<TcpPacket>();
            if (tcp is null) { key = default; return false; }

            var ip = tcp.ParentPacket as IPPacket;
            var srcIp = ip?.SourceAddress ?? IPAddress.None;
            var dstIp = ip?.DestinationAddress ?? IPAddress.None;

            // An outbound frame presents the same stream mirrored — the server
            // is the destination — so the key is folded onto the server's side.
            key = _classify(srcIp, tcp.SourcePort, dstIp, tcp.DestinationPort)
                    == Model.Direction.Outbound
                ? new StreamKey(dstIp, tcp.DestinationPort, tcp.SourcePort)
                : new StreamKey(srcIp, tcp.SourcePort, tcp.DestinationPort);
            return true;
        }
        catch (Exception)
        {
            key = default;
            return false;
        }
    }

    public void Dispose()
    {
        _inner.FrameReceived -= OnFrame;
        _inner.EndOfStream -= OnEndOfStream;
        _inner.Dispose();
    }
}
