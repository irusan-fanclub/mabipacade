using System.Net;
using Mabipacade.Core.Diagnostics;

namespace Mabipacade.Core.Capture;

public sealed class CaptureSession
{
    private readonly GameEndpointResolver _resolver;
    private GameEndpoint? _current;
    private IPEndPoint? _lastRemote;

    public event EventHandler<SessionEvent>? SessionEventReceived;

    public CaptureSession(ITcpConnectionTable table, int? processPid, RegionProfile? region)
    {
        _resolver = new GameEndpointResolver(table, processPid, region);
    }

    public GameEndpoint? Current => _current;

    public void PollOnce()
    {
        var next = _resolver.TryResolveOnce();
        if (next is null && _current is not null)
        {
            SessionEventReceived?.Invoke(this, new SessionEvent.ConnectionLost(
                DateTime.UtcNow, new IPEndPoint(_current.RemoteAddress, _current.RemotePort)));
            _lastRemote = new IPEndPoint(_current.RemoteAddress, _current.RemotePort);
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

    public async Task RunAsync(TimeSpan interval, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            PollOnce();
            try { await Task.Delay(interval, ct); }
            catch (OperationCanceledException) { return; }
        }
    }
}
