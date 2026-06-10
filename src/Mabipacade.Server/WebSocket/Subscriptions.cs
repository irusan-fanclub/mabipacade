using System.Globalization;
using System.Text.Json;
using Mabipacade.Core.Diagnostics;
using Mabipacade.Core.Model;

namespace Mabipacade.Server.WebSocket;

internal sealed class Subscription
{
    private HashSet<string>? _kinds;       // null = all kinds
    private HashSet<uint>? _ops;         // null = all ops

    public void ApplyCommand(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("op", out var opElem)) return;
            if (opElem.GetString() != "subscribe") return;

            _kinds = ReadStringArray(root, "kinds");

            if (root.TryGetProperty("ops", out var opsElem) && opsElem.ValueKind == JsonValueKind.Array)
            {
                var ops = new HashSet<uint>();
                foreach (var item in opsElem.EnumerateArray())
                {
                    var s = item.GetString();
                    if (s is null) continue;
                    var hex = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? s[2..] : s;
                    if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var op))
                        ops.Add(op);
                }
                _ops = ops.Count == 0 ? null : ops;
            }
            else
            {
                _ops = null;
            }
        }
        catch { /* malformed command — keep current subscription */ }
    }

    public bool AllowsPacket(MabiPacket p)
    {
        if (_kinds is not null && !_kinds.Contains("packet")) return false;
        if (_ops is not null && !_ops.Contains(p.Op)) return false;
        return true;
    }

    public bool AllowsEvent(SessionEvent _)
    {
        if (_kinds is not null && !_kinds.Contains("event")) return false;
        return true;
    }

    private static HashSet<string>? ReadStringArray(JsonElement root, string propName)
    {
        if (!root.TryGetProperty(propName, out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
        var set = new HashSet<string>();
        foreach (var item in arr.EnumerateArray())
        {
            var s = item.GetString();
            if (s is not null) set.Add(s);
        }
        return set.Count == 0 ? null : set;
    }
}
