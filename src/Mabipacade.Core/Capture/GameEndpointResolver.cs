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

    public GameEndpoint? TryResolveOnce()
    {
        var rows = _table.GetConnections();

        IEnumerable<TcpConnectionRow> candidates = rows
            .Where(r => r.State == TcpConnectionState.Established);

        if (_processPid is int pid)
        {
            var owned = candidates.Where(r => r.OwningPid == pid).ToList();
            if (owned.Count > 0) return Pick(owned);
        }

        if (_region is not null)
        {
            var inRange = candidates.Where(r => _region.Contains(r.RemoteAddress, r.RemotePort)).ToList();
            if (inRange.Count > 0) return Pick(inRange);
        }

        return null;
    }

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
