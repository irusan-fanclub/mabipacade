using System.Net;
using System.Net.Sockets;

namespace Mabipacade.Core.Capture;

/// <summary>
/// Builds the live capture filter from the game process's own connections.
///
/// The filter covers each server's /24 rather than the exact socket. A channel
/// switch opens a new connection, usually to a different address inside the same
/// network, and its local port is not knowable until the next poll of the TCP
/// table — too late for the snapshot the server sends immediately on connect.
/// Covering the network means the new connection is captured the instant it
/// opens; deciding whether a stream is actually ours is left to the local-port
/// vet downstream.
/// </summary>
public static class BpfFilter
{
    /// <summary>Web traffic on the game's own hosts: CDN downloads, not gameplay.</summary>
    private static readonly ushort[] WebPorts = { 80, 443 };

    /// <summary>
    /// The filter for these connections, or null when none qualifies — null
    /// means "no opinion", so a caller keeps its current filter rather than
    /// installing one that matches nothing. <paramref name="bothDirections"/>
    /// widens each term from server→client only to either direction, which is
    /// what admits the client's own packets.
    /// </summary>
    public static string? ForConnections(IReadOnlyList<TcpConnectionRow> rows, bool bothDirections = false)
    {
        var networks = rows
            .Where(r => r.State == TcpConnectionState.Established)
            .Where(r => !WebPorts.Contains(r.RemotePort))
            .Select(r => Network24(r.RemoteAddress))
            .OfType<string>()
            .Distinct()
            // Sorted so an unchanged set of connections yields an identical
            // string, which is what lets the watchdog skip a needless SetFilter.
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var qualifier = bothDirections ? "net " : "src net ";
        return networks.Count == 0
            ? null
            : "tcp and (" + string.Join(" or ", networks.Select(n => qualifier + n)) + ")";
    }

    /// <summary>
    /// The filter for a single server address, used at startup when the resolved
    /// endpoint is known but the connection list has not settled yet. Null for
    /// anything that is not IPv4.
    /// </summary>
    public static string? ForAddress(IPAddress address, bool bothDirections = false) =>
        Network24(address) is { } net
            ? $"tcp and ({(bothDirections ? "net" : "src net")} {net})"
            : null;

    /// <summary>The address's /24, or null for anything that is not IPv4.</summary>
    private static string? Network24(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork) return null;
        var b = address.GetAddressBytes();
        return $"{b[0]}.{b[1]}.{b[2]}.0/24";
    }
}
