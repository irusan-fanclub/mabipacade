namespace Mabipacade.Core.Capture;

/// <summary>
/// Answers which local TCP ports belong to the game process.
///
/// This is the only thing that separates our traffic from a NAT'd virtual
/// machine's: the VM's packets leave through the hypervisor's NAT service, so on
/// the wire they carry the host's address and differ only in the local port,
/// which the TCP table maps back to an owning process.
/// </summary>
public sealed class ClientConnectionTracker
{
    /// <summary>
    /// How long a port set is trusted before being re-read. Short enough that a
    /// closed socket stops being honoured quickly, long enough that a burst of
    /// new streams does not turn into a burst of syscalls.
    /// </summary>
    private static readonly TimeSpan DefaultCacheMaxAge = TimeSpan.FromSeconds(2);


    private readonly ITcpConnectionTable _table;
    private readonly int? _processId;
    private readonly TimeSpan _cacheMaxAge;
    private readonly Func<DateTime> _clock;
    private readonly object _gate = new();

    private HashSet<ushort>? _ports;
    private DateTime _polledAt;

    public ClientConnectionTracker(
        ITcpConnectionTable table,
        int? processId,
        TimeSpan? cacheMaxAge = null,
        Func<DateTime>? clock = null)
    {
        _table = table;
        _processId = processId;
        _cacheMaxAge = cacheMaxAge ?? DefaultCacheMaxAge;
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    /// <summary>
    /// Whether this local port belongs to the game process.
    ///
    /// An unknown port always re-reads the table before answering. A channel
    /// switch opens its socket between scheduled polls, and making it wait would
    /// drop the snapshot the server sends immediately on connect — the exact
    /// data a switch exists to deliver.
    ///
    /// That makes every miss a syscall, so callers must decide per connection,
    /// not per frame. <see cref="Sources.ClientTrafficFilterSource"/> caches its
    /// verdict per stream for this reason; calling this on the packet path
    /// instead would read the table for every frame of foreign traffic.
    /// </summary>
    public bool IsClientLocalPort(ushort port)
    {
        // With no process to attribute ports to there is no basis for rejecting
        // anything, so everything is admitted.
        if (_processId is null) return true;

        lock (_gate)
        {
            if (_ports is not null && !IsOlderThan(_cacheMaxAge) && _ports.Contains(port))
                return true;

            // Losing game data is worse than recording a little extra, so an
            // unreadable table admits the frame rather than dropping it.
            if (!TryRefresh()) return true;

            return _ports!.Contains(port);
        }
    }

    /// <summary>
    /// The game process's current connections, for building the capture filter.
    /// Empty when the table cannot be read, which leaves the caller's existing
    /// filter in place.
    /// </summary>
    public IReadOnlyList<TcpConnectionRow> Snapshot()
    {
        try
        {
            return Owned(_table.GetConnections());
        }
        catch (Exception)
        {
            return Array.Empty<TcpConnectionRow>();
        }
    }

    private bool IsOlderThan(TimeSpan age) => _clock() - _polledAt >= age;

    /// <summary>Re-reads the table. False when it could not be read at all.</summary>
    private bool TryRefresh()
    {
        IReadOnlyList<TcpConnectionRow> rows;
        try
        {
            rows = _table.GetConnections();
        }
        catch (Exception)
        {
            return false;
        }

        var ports = new HashSet<ushort>();
        foreach (var r in Owned(rows)) ports.Add(r.LocalPort);
        _ports = ports;
        _polledAt = _clock();
        return true;
    }

    private List<TcpConnectionRow> Owned(IReadOnlyList<TcpConnectionRow> rows) =>
        rows.Where(r => _processId is null || r.OwningPid == _processId).ToList();
}
