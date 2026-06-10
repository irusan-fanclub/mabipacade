using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Fleck;
using Mabipacade.Core.Diagnostics;
using Mabipacade.Core.Json;
using Mabipacade.Core.Model;
using Mabipacade.Server.Logging;

namespace Mabipacade.Server.WebSocket;

internal sealed class WebSocketHost : IDisposable
{
    private readonly WebSocketServer _server;
    private readonly ConcurrentDictionary<IWebSocketConnection, Subscription> _clients = new();
    private readonly Func<uint, string?> _opNameLookup;

    public WebSocketHost(int port, Func<uint, string?> opNameLookup)
    {
        _opNameLookup = opNameLookup;
        FleckLog.Level = LogLevel.Warn;
        _server = new WebSocketServer($"ws://127.0.0.1:{port}");
    }

    public void Start()
    {
        _server.Start(socket =>
        {
            socket.OnOpen = () =>
            {
                _clients[socket] = new Subscription();
                StderrLogger.Info($"client connected: {socket.ConnectionInfo.ClientIpAddress}");
            };
            socket.OnClose = () =>
            {
                _clients.TryRemove(socket, out _);
                StderrLogger.Info("client disconnected");
            };
            socket.OnMessage = message =>
            {
                if (_clients.TryGetValue(socket, out var sub))
                {
                    sub.ApplyCommand(message);
                    StderrLogger.Info("client subscription updated");
                }
            };
        });
        StderrLogger.Info($"listening on {_server.Location}");
    }

    public void BroadcastPacket(MabiPacket p)
    {
        var json = RenderPacket(p);
        foreach (var (socket, sub) in _clients)
        {
            if (sub.AllowsPacket(p))
            {
                _ = socket.Send(json).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        StderrLogger.Warn($"send failed: {t.Exception?.GetBaseException().Message}");
                }, TaskContinuationOptions.OnlyOnFaulted);
            }
        }
    }

    public void BroadcastEvent(SessionEvent ev)
    {
        var json = RenderEvent(ev);
        foreach (var (socket, sub) in _clients)
        {
            if (sub.AllowsEvent(ev))
            {
                _ = socket.Send(json).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        StderrLogger.Warn($"send failed: {t.Exception?.GetBaseException().Message}");
                }, TaskContinuationOptions.OnlyOnFaulted);
            }
        }
    }

    private string RenderPacket(MabiPacket p)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            EnvelopeShape.WritePacket(w, p, _opNameLookup);
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private string RenderEvent(SessionEvent ev)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            EnvelopeShape.WriteEvent(w, ev);
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public void Dispose() => _server.Dispose();
}
