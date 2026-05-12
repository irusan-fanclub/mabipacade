using PacketDotNet;
using Mabipacade.Core.Diagnostics;
using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Core.Sources;

namespace Mabipacade.Core.Pipeline;

public sealed class PacketPipeline : IDisposable
{
    private readonly IFrameSource _source;
    private readonly DecoderRegistry _decoders;
    private readonly TcpReassembler _reassembler = new();
    private readonly PipelineMetrics _metrics = new();

    public event EventHandler<MabiPacket>? PacketReceived;
    public event EventHandler<SessionEvent>? SessionEventReceived;
    public PipelineMetrics Metrics => _metrics;

    public PacketPipeline(IFrameSource source, DecoderRegistry decoders)
    {
        _source = source;
        _decoders = decoders;
        _source.FrameReceived += OnFrame;
        _source.EndOfStream += OnEos;
    }

    public Task StartAsync(CancellationToken ct) => _source.StartAsync(ct);
    public Task StopAsync() => _source.StopAsync();

    private void OnFrame(object? sender, RawFrameEventArgs e)
    {
        _metrics.IncrementFrames();
        var parsed = Packet.ParsePacket(e.LinkLayer, e.Data);
        var tcp = parsed.Extract<TcpPacket>();
        if (tcp?.PayloadData is not { Length: > 0 } payload) return;

        var ip = tcp.ParentPacket as IPPacket;
        var frame = new TcpFrame(
            ip?.SourceAddress ?? System.Net.IPAddress.None,
            tcp.SourcePort,
            ip?.DestinationAddress ?? System.Net.IPAddress.None,
            tcp.DestinationPort,
            tcp.SequenceNumber,
            payload,
            e.TimestampUtc);

        _reassembler.Feed(frame);
        DrainPackets(e.TimestampUtc);
    }

    private void DrainPackets(DateTime timestampUtc)
    {
        while (true)
        {
            var buf = _reassembler.GetBuffer();
            if (buf.Length == 0) return;

            var result = MabiPacketFramer.TryReadOne(buf, out var slice, out int consumed);
            switch (result)
            {
                case FrameResult.Ok:
                    _reassembler.Consume(consumed);
                    if (slice is not null) EmitPacket(slice, timestampUtc);
                    break;
                case FrameResult.NeedMore:
                    return;
                case FrameResult.FramingError:
                    _metrics.IncrementResync();
                    SessionEventReceived?.Invoke(this, new SessionEvent.FrameResync(timestampUtc, 0, "framing error"));
                    _reassembler.Reset();
                    return;
            }
        }
    }

    private void EmitPacket(MabiPacketSlice slice, DateTime timestampUtc)
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
                decoded = decoder.Decode(new DecoderInput(timestampUtc, Direction.Inbound, slice.Op, slice.EntityId, elems));
            }
            catch (Exception ex)
            {
                SessionEventReceived?.Invoke(this, new SessionEvent.DecoderFailed(timestampUtc, slice.Op, ex.Message));
            }
        }

        _metrics.IncrementPackets();
        PacketReceived?.Invoke(this, new MabiPacket(timestampUtc, Direction.Inbound, slice.Op, slice.EntityId, elems, decoded));
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
