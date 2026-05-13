using System.Net;
using System.Text.Json;
using Mabipacade.Core.Diagnostics;
using Mabipacade.Core.Model;

namespace Mabipacade.Core.Json;

public static class EnvelopeShape
{
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static void WritePacket(Utf8JsonWriter w, MabiPacket p, Func<ushort, string?>? opNameLookup = null)
    {
        w.WriteStartObject();
        w.WriteString("kind", "packet");
        w.WriteString("ts", p.TimestampUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        w.WriteString("dir", p.Direction == Direction.Inbound ? "in" : "out");
        w.WriteString("op", $"0x{p.Op:X4}");

        var name = opNameLookup?.Invoke(p.Op);
        if (name is null) w.WriteNull("opName"); else w.WriteString("opName", name);

        w.WriteString("entityId", p.EntityId.ToString());

        if (p.Decoded is null)
        {
            w.WriteNull("type");
            w.WriteNull("decoded");
        }
        else
        {
            w.WriteString("type", p.Decoded.GetType().Name);
            w.WritePropertyName("decoded");
            JsonSerializer.Serialize(w, p.Decoded, p.Decoded.GetType(), PayloadOptions);
        }

        w.WritePropertyName("elems");
        w.WriteStartArray();
        foreach (var e in p.Elems) ElemJson.Write(w, e);
        w.WriteEndArray();

        w.WriteEndObject();
    }

    public static void WriteEvent(Utf8JsonWriter w, SessionEvent ev)
    {
        w.WriteStartObject();
        w.WriteString("kind", "event");
        w.WriteString("ts", ev.TimestampUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        w.WriteString("type", ev.GetType().Name);

        switch (ev)
        {
            case SessionEvent.SessionStart s:
                w.WriteString("region", s.Region);
                if (s.ProcessId is null) w.WriteNull("processId"); else w.WriteNumber("processId", s.ProcessId.Value);
                break;
            case SessionEvent.SessionEnd s:
                w.WriteString("reason", s.Reason);
                break;
            case SessionEvent.ConnectionEstablished s:
                w.WriteString("remote", FormatEndpoint(s.Remote));
                w.WriteString("nicName", s.NicName);
                break;
            case SessionEvent.ConnectionLost s:
                w.WriteString("lastRemote", FormatEndpoint(s.LastRemote));
                break;
            case SessionEvent.ConnectionResumed s:
                w.WriteString("newRemote", FormatEndpoint(s.NewRemote));
                w.WriteBoolean("sameAsLast", s.SameAsLast);
                break;
            case SessionEvent.FrameResync s:
                w.WriteNumber("byteOffset", s.ByteOffset);
                w.WriteString("reason", s.Reason);
                break;
            case SessionEvent.BadBody s:
                w.WriteString("op", $"0x{s.Op:X4}");
                w.WriteNumber("length", s.Length);
                break;
            case SessionEvent.DecoderFailed s:
                w.WriteString("op", $"0x{s.Op:X4}");
                w.WriteString("exceptionMessage", s.ExceptionMessage);
                break;
        }

        w.WriteEndObject();
    }

    private static string FormatEndpoint(IPEndPoint ep) => $"{ep.Address}:{ep.Port}";
}
