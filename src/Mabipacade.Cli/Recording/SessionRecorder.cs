using System.Net;
using System.Text;
using Mabipacade.Core.Json;
using Mabipacade.Core.Diagnostics;
using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Recording;
using Mabipacade.Core.Sources;
using PacketDotNet;

namespace Mabipacade.Cli.Recording;

internal sealed class SessionRecorder : IDisposable
{
    private readonly string _dir;
    private readonly string _region;
    private readonly int? _processId;
    private readonly IFrameSource _source;
    private readonly PacketPipeline _pipeline;
    private readonly PcapWriter _pcap;
    private readonly StreamWriter _events;
    private readonly NdjsonWriter _eventsWriter;
    private readonly DateTime _startedAt = DateTime.UtcNow;
    private readonly List<SessionEndpointRecord> _endpoints = new();
    private DateTime? _endedAt;
    private string? _currentRemote;
    private DateTime _currentSince;

    public SessionRecorder(string dir, string region, int? processId,
        IFrameSource source, PacketPipeline pipeline, LinkLayers linkLayer)
    {
        _dir = dir;
        _region = region;
        _processId = processId;
        _source = source;
        _pipeline = pipeline;
        Directory.CreateDirectory(dir);
        _pcap = new PcapWriter(Path.Combine(dir, "session.pcap"), linkLayer);
        _events = new StreamWriter(Path.Combine(dir, "session.events.ndjson"), append: false, Encoding.UTF8);
        _eventsWriter = new NdjsonWriter(_events);
    }

    public void Start()
    {
        _source.FrameReceived += OnFrame;
        _pipeline.SessionEventReceived += OnEvent;
        _eventsWriter.WriteEvent(new SessionEvent.SessionStart(_startedAt, _region, _processId));
    }

    public void Stop(string reason)
    {
        if (_endedAt is not null) return;
        _endedAt = DateTime.UtcNow;
        if (_currentRemote is not null)
        {
            _endpoints.Add(new SessionEndpointRecord(_currentRemote, _currentSince, _endedAt));
            _currentRemote = null;
        }
        _eventsWriter.WriteEvent(new SessionEvent.SessionEnd(_endedAt.Value, reason));
        _source.FrameReceived -= OnFrame;
        _pipeline.SessionEventReceived -= OnEvent;
        _events.Flush();
        _events.Dispose();
        _pcap.Dispose();
        WriteSessionJson();
    }

    public void WriteEvent(SessionEvent ev) => _eventsWriter.WriteEvent(ev);

    private void OnFrame(object? sender, RawFrameEventArgs e) =>
        _pcap.Write(e.Data, e.TimestampUtc);

    private void OnEvent(object? sender, SessionEvent ev)
    {
        _eventsWriter.WriteEvent(ev);
        switch (ev)
        {
            case SessionEvent.ConnectionEstablished s:
                _currentRemote = $"{s.Remote.Address}:{s.Remote.Port}";
                _currentSince = s.TimestampUtc;
                break;
            case SessionEvent.ConnectionLost s:
                if (_currentRemote is not null)
                {
                    _endpoints.Add(new SessionEndpointRecord(_currentRemote, _currentSince, s.TimestampUtc));
                    _currentRemote = null;
                }
                break;
            case SessionEvent.ConnectionResumed s:
                _currentRemote = $"{s.NewRemote.Address}:{s.NewRemote.Port}";
                _currentSince = s.TimestampUtc;
                break;
        }
    }

    private void WriteSessionJson()
    {
        var meta = new SessionMetadata(
            Id: _startedAt.ToString("yyyy-MM-ddTHH-mm-ss"),
            StartedAt: _startedAt,
            EndedAt: _endedAt,
            Region: _region,
            Endpoints: _endpoints,
            Stats: new SessionStats(
                _pipeline.Metrics.TotalFrames,
                _pipeline.Metrics.TotalPackets,
                _pipeline.Metrics.BadBodyCount,
                _pipeline.Metrics.FrameResyncCount));
        File.WriteAllText(Path.Combine(_dir, "session.json"), SessionMetadata.ToJson(meta), Encoding.UTF8);
    }

    public void Dispose()
    {
        if (_endedAt is null) Stop("Disposed");
    }
}
