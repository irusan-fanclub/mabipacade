using System.Net;
using Mabipacade.Core.Diagnostics;

namespace Mabipacade.Core.Capture;

/// <summary>
/// Watches the game process's connections while a capture runs.
///
/// It does two things on each poll: keeps the capture filter equal to the
/// networks the client is actually talking to, so a channel switch or a
/// retargeted accelerator does not silently end the capture; and reports
/// endpoint changes as session events, so a log read afterwards shows where a
/// switch happened.
///
/// Applying the filter is a callback rather than a device reference: the
/// watchdog then has no opinion about how capture is implemented, and its
/// behaviour is testable without one.
/// </summary>
public sealed class CaptureSession
{
    /// <summary>mogugi's cadence, fast enough that a new connection is covered almost at once.</summary>
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(500);

    private readonly ITcpConnectionTable _table;
    private readonly int? _processPid;
    private readonly RegionProfile? _region;
    private readonly Action<string>? _applyFilter;
    private readonly bool _bothDirections;

    private GameEndpoint? _current;
    private IPEndPoint? _lastRemote;
    private string? _appliedFilter;

    public event EventHandler<SessionEvent>? SessionEventReceived;

    public CaptureSession(
        ITcpConnectionTable table,
        int? processPid,
        RegionProfile? region,
        Action<string>? applyFilter = null,
        bool bothDirections = false)
    {
        _table = table;
        _processPid = processPid;
        _region = region;
        _applyFilter = applyFilter;
        _bothDirections = bothDirections;
    }

    public GameEndpoint? Current => _current;

    /// <summary>The filter last handed to <c>applyFilter</c>, or null before the first one.</summary>
    public string? AppliedFilter => _appliedFilter;

    public void PollOnce()
    {
        IReadOnlyList<TcpConnectionRow> rows;
        try
        {
            rows = _table.GetConnections();
        }
        catch (Exception)
        {
            // A momentary failure to read the table says nothing about the
            // connection; leave the filter and the endpoint as they are.
            return;
        }

        UpdateFilter(rows);
        UpdateEndpoint(rows);
    }

    public async Task RunAsync(TimeSpan interval, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            PollOnce();
            try { await Task.Delay(interval, ct); }
            catch (OperationCanceledException) { return; }
        }
    }

    public Task RunAsync(CancellationToken ct) => RunAsync(DefaultPollInterval, ct);

    private void UpdateFilter(IReadOnlyList<TcpConnectionRow> rows)
    {
        if (_applyFilter is null) return;

        var owned = _processPid is int pid
            ? rows.Where(r => r.OwningPid == pid).ToList()
            : rows.ToList();

        var wanted = BpfFilter.ForConnections(owned, _bothDirections);
        // Null means nothing qualified. A filter matching nothing is worse than
        // one that is merely stale, so the current one stays.
        if (wanted is null || wanted == _appliedFilter) return;

        try
        {
            _applyFilter(wanted);
            _appliedFilter = wanted;
        }
        catch (Exception)
        {
            // Keep the previous filter and try again on the next poll. Failing
            // to narrow the capture must never stop it.
        }
    }

    private void UpdateEndpoint(IReadOnlyList<TcpConnectionRow> rows)
    {
        var next = new GameEndpointResolver(_table, _processPid, _region).Resolve(rows);

        if (next is null && _current is not null)
        {
            var lost = new IPEndPoint(_current.RemoteAddress, _current.RemotePort);
            SessionEventReceived?.Invoke(this, new SessionEvent.ConnectionLost(DateTime.UtcNow, lost));
            _lastRemote = lost;
            _current = null;
            return;
        }

        if (next is not null && _current is null)
        {
            var nextRemote = new IPEndPoint(next.RemoteAddress, next.RemotePort);
            if (_lastRemote is null)
                SessionEventReceived?.Invoke(this, new SessionEvent.ConnectionEstablished(DateTime.UtcNow, nextRemote, ""));
            else
                SessionEventReceived?.Invoke(this, new SessionEvent.ConnectionResumed(
                    DateTime.UtcNow, nextRemote, SameAsLast: nextRemote.Equals(_lastRemote)));
            _current = next;
        }
    }
}
