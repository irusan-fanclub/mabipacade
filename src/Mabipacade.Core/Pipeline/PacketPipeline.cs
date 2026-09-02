using System.Buffers.Binary;
using PacketDotNet;
using Mabipacade.Core.Crypto;
using Mabipacade.Core.Diagnostics;
using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Core.Sources;

namespace Mabipacade.Core.Pipeline;

public sealed class PacketPipeline : IDisposable
{
    private readonly IFrameSource _source;
    private readonly DecoderRegistry _decoders;
    private readonly DirectionClassifier _classify;
    private readonly bool _decryptOutbound;
    /// <summary>Length of the encryption key a connection opens with.</summary>
    private const int ConnectionKeyLength = 4;

    private readonly Dictionary<FlowKey, TcpReassembler> _reassemblers = new();
    /// <summary>Per-connection client cipher, seeded from the connection's first inbound bytes.</summary>
    private readonly Dictionary<ConnKey, MabiClientCipher> _ciphers = new();
    private readonly PipelineMetrics _metrics = new();

    public event EventHandler<MabiPacket>? PacketReceived;
    public event EventHandler<SessionEvent>? SessionEventReceived;
    public PipelineMetrics Metrics => _metrics;

    /// <param name="decryptOutbound">
    /// When true, client→server packets are decrypted with Mabinogi's client
    /// cipher, keyed by the seed the server sends at connection open. This only
    /// works for connections captured from the start (a fresh login or a channel
    /// switch); on a connection joined mid-stream the seed is missed and its
    /// outbound packets resync as before. Requires a classifier that can tell
    /// the server end, so outbound flows are recognised.
    /// </param>
    public PacketPipeline(IFrameSource source, DecoderRegistry decoders,
        DirectionClassifier? directionClassifier = null, bool decryptOutbound = false)
    {
        _source = source;
        _decoders = decoders;
        _classify = directionClassifier ?? DirectionClassifiers.InboundOnly;
        _decryptOutbound = decryptOutbound;
        _source.FrameReceived += OnFrame;
        _source.EndOfStream += OnEos;
    }

    public Task StartAsync(CancellationToken ct) => _source.StartAsync(ct);
    public Task StopAsync() => _source.StopAsync();

    private readonly record struct FlowKey(System.Net.IPAddress SrcIp, ushort SrcPort, System.Net.IPAddress DstIp, ushort DstPort);

    /// <summary>A TCP connection identified from the server's side, so both directions map to one key.</summary>
    private readonly record struct ConnKey(System.Net.IPAddress ServerIp, ushort ServerPort, System.Net.IPAddress ClientIp, ushort ClientPort);

    private void OnFrame(object? sender, RawFrameEventArgs e)
    {
        _metrics.IncrementFrames();

        TcpFrame frame;
        try
        {
            var parsed = Packet.ParsePacket(e.LinkLayer, e.Data);
            var tcp = parsed.Extract<TcpPacket>();
            if (tcp?.PayloadData is not { Length: > 0 } payload) return;

            var ip = tcp.ParentPacket as IPPacket;
            var srcIp = ip?.SourceAddress ?? System.Net.IPAddress.None;
            var dstIp = ip?.DestinationAddress ?? System.Net.IPAddress.None;
            frame = new TcpFrame(srcIp, tcp.SourcePort, dstIp, tcp.DestinationPort,
                tcp.SequenceNumber, payload, e.TimestampUtc);
        }
        catch (Exception)
        {
            // A frame cut mid-header — a foreign capture taken with a small
            // snaplen — is unparseable. PacketDotNet decodes lazily, so the
            // failure surfaces on field access rather than at ParsePacket,
            // which is why the whole extraction sits inside the try. Dropping
            // it here matters because this runs on the capture callback, where
            // an escaping exception ends the capture and loses the rest of the
            // file. Reassembly stays outside: a fault there is our bug, not a
            // malformed frame, and must not be swallowed.
            _metrics.IncrementMalformedFrame();
            return;
        }

        // Direction is a property of the flow, so classifying per frame gives
        // every packet drained from this reassembler the right label.
        var direction = _classify(frame.SrcIp, frame.SrcPort, frame.DstIp, frame.DstPort);

        // The 4-byte connection key a stream opens with is the cipher seed. It
        // must be read here, before the skip below discards it — capture then
        // draws the seed from whichever inbound bytes open the connection.
        MabiClientCipher? cipher = null;
        if (_decryptOutbound)
        {
            var conn = ConnKeyFor(frame, direction);
            if (direction == Direction.Inbound) TryCaptureSeed(conn, frame.Payload);
            else _ciphers.TryGetValue(conn, out cipher);
        }

        var key = new FlowKey(frame.SrcIp, frame.SrcPort, frame.DstIp, frame.DstPort);
        if (!_reassemblers.TryGetValue(key, out var reassembler))
        {
            reassembler = new TcpReassembler();
            _reassemblers[key] = reassembler;

            // A connection opens with a 4-byte encryption key that carries no
            // game data. Framing it would read a garbage length, reset the
            // stream, and take the first real packet down with it — which after
            // a channel switch is the full-character snapshot. A game packet
            // cannot be this short: its header alone is 6 bytes. Leaving the
            // reassembler unfed lets the next frame anchor the stream.
            if (frame.Payload.Length == ConnectionKeyLength) return;
        }

        reassembler.Feed(frame);
        DrainPackets(reassembler, e.TimestampUtc, direction, cipher);
    }

    /// <summary>The connection a frame belongs to, folded onto the server side so both directions agree.</summary>
    private ConnKey ConnKeyFor(TcpFrame f, Direction direction) =>
        direction == Direction.Inbound
            ? new ConnKey(f.SrcIp, f.SrcPort, f.DstIp, f.DstPort)   // src is the server
            : new ConnKey(f.DstIp, f.DstPort, f.SrcIp, f.SrcPort);  // dst is the server

    /// <summary>
    /// Derives the client cipher from the connection's opening bytes, once. The
    /// server sends the seed as the first four bytes it writes (little-endian);
    /// on a connection we joined mid-stream those bytes are ordinary traffic and
    /// the resulting keystream just yields packets that fail to frame — no worse
    /// than leaving them encrypted.
    /// </summary>
    private void TryCaptureSeed(ConnKey conn, byte[] firstInboundPayload)
    {
        if (_ciphers.ContainsKey(conn) || firstInboundPayload.Length < 4) return;
        uint seed = BinaryPrimitives.ReadUInt32LittleEndian(firstInboundPayload);
        _ciphers[conn] = new MabiClientCipher(seed);
    }

    private void DrainPackets(TcpReassembler reassembler, DateTime timestampUtc, Direction direction, MabiClientCipher? cipher)
    {
        while (true)
        {
            var buf = reassembler.GetBuffer();
            if (buf.Length == 0) return;

            MabiPacketSlice? slice;
            int consumed;
            var result = cipher is null
                ? MabiPacketFramer.TryReadOne(buf, out slice, out consumed)
                : TryReadOutbound(buf, cipher, out slice, out consumed);
            switch (result)
            {
                case FrameResult.Ok:
                    reassembler.Consume(consumed);
                    if (slice is not null) EmitPacket(slice, timestampUtc, direction);
                    break;
                case FrameResult.NeedMore:
                    return;
                case FrameResult.FramingError:
                    _metrics.IncrementResync();
                    SessionEventReceived?.Invoke(this, new SessionEvent.FrameResync(timestampUtc, reassembler.BytesConsumed, "framing error"));
                    reassembler.Reset();
                    return;
            }
        }
    }

    /// <summary>
    /// Frames one client→server packet, decrypting it first.
    ///
    /// The header (sign/length/flag) travels in the clear, so the length is
    /// always trustworthy and framing is done by length directly rather than by
    /// the inbound framer — client control packets (the login handshake, short
    /// heartbeats) are far shorter than any server packet and would trip its
    /// minimum-size check. The body is decrypted in a copy and the 4-byte
    /// checksum trailer is dropped. Every encrypted packet advances the cipher,
    /// so the whole stream must pass through here in order; short packets are
    /// still decrypted, then frame to nothing.
    /// </summary>
    private static FrameResult TryReadOutbound(ReadOnlySpan<byte> buffer, MabiClientCipher cipher,
        out MabiPacketSlice? slice, out int consumed)
    {
        const int headerSize = 6;
        const int checksumSize = 4;
        const int opAndEntity = 4 + 8;
        slice = null;
        consumed = 0;

        if (buffer.Length < headerSize) return FrameResult.NeedMore;

        uint length = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(1, 4));
        byte flag = buffer[5];
        // A sane length is the only thing that can fail: a wrong seed corrupts
        // the body, never this plaintext field. A garbage length means the
        // stream is off a packet boundary (joined mid-stream), so resync.
        if (length < headerSize + checksumSize || length > 0x100_0000) return FrameResult.FramingError;
        if (buffer.Length < length) return FrameResult.NeedMore;

        // Copy the on-wire packet so it can be decrypted without disturbing the
        // reassembler's buffer, then advance past the whole thing.
        var packet = buffer.Slice(0, (int)length).ToArray();
        if (flag != MabiClientCipher.UnencryptedFlag)
            cipher.DecryptPacket(packet);
        consumed = (int)length;

        // Short packets (heartbeats) and control packets too small to hold a
        // game body decrypt to advance the cipher but yield no packet.
        bool isShort = flag == 1 || flag == 2;
        int bodyLen = (int)length - checksumSize - headerSize;
        if (isShort || bodyLen < opAndEntity) return FrameResult.Ok;

        uint op = BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(headerSize, 4));
        ulong entityId = BinaryPrimitives.ReadUInt64BigEndian(packet.AsSpan(headerSize + 4, 8));
        var msg = packet.AsSpan(headerSize + opAndEntity, bodyLen - opAndEntity).ToArray();
        slice = new MabiPacketSlice(op, entityId, msg);
        return FrameResult.Ok;
    }

    private void EmitPacket(MabiPacketSlice slice, DateTime timestampUtc, Direction direction)
    {
        var elemResult = MessageElemReader.TryRead(slice.Body, out var elems);
        if (elemResult == ReadElemsResult.BadBody)
        {
            _metrics.IncrementBadBody();
            SessionEventReceived?.Invoke(this, new SessionEvent.BadBody(timestampUtc, slice.Op, slice.Body.Length));
            return;
        }

        object? decoded = null;
        if (_decoders.TryGet(slice.Op, out var decoder) && decoder is not null)
        {
            try
            {
                decoded = decoder.Decode(new DecoderInput(timestampUtc, direction, slice.Op, slice.EntityId, elems));
            }
            catch (Exception ex)
            {
                SessionEventReceived?.Invoke(this, new SessionEvent.DecoderFailed(timestampUtc, slice.Op, ex.Message));
            }
        }

        _metrics.IncrementPackets();
        PacketReceived?.Invoke(this, new MabiPacket(timestampUtc, direction, slice.Op, slice.EntityId, elems, decoded) { Body = slice.Body });
    }

    private void OnEos(object? sender, EventArgs e)
    {
        SessionEventReceived?.Invoke(this, new SessionEvent.SessionEnd(DateTime.UtcNow, "EndOfStream"));
    }

    public void Dispose()
    {
        _source.FrameReceived -= OnFrame;
        _source.EndOfStream -= OnEos;
        _source.Dispose();
    }
}
