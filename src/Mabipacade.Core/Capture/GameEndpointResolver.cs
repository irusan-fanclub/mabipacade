using System.Net;

namespace Mabipacade.Core.Capture;

public sealed class GameEndpointResolver
{
    private readonly ITcpConnectionTable _table;
    private readonly int? _processPid;
    private readonly RegionProfile? _region;

    public GameEndpointResolver(ITcpConnectionTable table, int? processPid, RegionProfile? region)
    {
        _table = table;
        _processPid = processPid;
        _region = region;
    }

    public GameEndpoint? TryResolveOnce() => Resolve(_table.GetConnections());

    /// <summary>
    /// Picks the endpoint from connections already read. The watchdog polls the
    /// table once per tick and uses the rows for both the filter and the
    /// endpoint, so this overload keeps it to a single read.
    /// </summary>
    public GameEndpoint? Resolve(IReadOnlyList<TcpConnectionRow> rows)
    {
        IEnumerable<TcpConnectionRow> candidates = rows
            .Where(r => r.State == TcpConnectionState.Established);

        if (_processPid is int pid)
        {
            var owned = candidates.Where(r => r.OwningPid == pid).ToList();
            if (owned.Count > 0) return Pick(Rank(owned));
        }

        if (_region is not null)
        {
            var inRange = candidates.Where(r => _region.Contains(r.RemoteAddress, r.RemotePort)).ToList();
            if (inRange.Count > 0) return Pick(Rank(inRange));
        }

        return null;
    }

    // The game process also holds non-game connections (auth, telemetry) whose
    // table order is arbitrary. A region match identifies the game stream when
    // the client reaches the servers directly; a network accelerator rewrites
    // the destination, so no profile can match and the tie-break falls to the
    // port split that holds either way — login/lobby on the low well-known
    // port, the game shard above it. Both are hints: neither drops a candidate,
    // so a rewritten endpoint stays resolvable.
    private IReadOnlyList<TcpConnectionRow> Rank(IReadOnlyList<TcpConnectionRow> rows) =>
        rows.OrderByDescending(r => _region?.Contains(r.RemoteAddress, r.RemotePort) == true)
            .ThenByDescending(r => r.RemotePort)
            .ToList();

    private static GameEndpoint Pick(IReadOnlyList<TcpConnectionRow> rows)
    {
        var preferred = rows.FirstOrDefault(r => r.RemotePort is not 80 and not 443) ?? rows[0];
        return new GameEndpoint(
            preferred.OwningPid,
            preferred.RemoteAddress,
            preferred.RemotePort,
            preferred.LocalAddress,
            preferred.LocalPort);
    }
}
