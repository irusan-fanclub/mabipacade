using System.Linq;
using Mabipacade.Core.Json;
using Mabipacade.Core.Model;

namespace Mabipacade.DebugUi.Services;

/// <summary>
/// Builds the export payloads for one or many selected packets. A single
/// packet keeps the established single-object shapes; a multi-selection
/// becomes a JSON array of the same objects, in the order given.
/// </summary>
public static class PacketExportBuilder
{
    /// <summary>Full envelopes, as the CLI log emits them.</summary>
    public static string BuildEnvelopeJson(IReadOnlyList<MabiPacket> packets) =>
        Build(packets, (w, p) => EnvelopeShape.WritePacket(w, p, Mabipacade.Decoders.OpCodeNames.TryGetName));

    /// <summary>Compact specimen shape: t / dir / op / opDec / eid / elems.</summary>
    public static string BuildSlimJson(IReadOnlyList<MabiPacket> packets) =>
        Build(packets, EnvelopeShape.WritePacketSlim);

    private static string Build(IReadOnlyList<MabiPacket> packets,
        Action<System.Text.Json.Utf8JsonWriter, MabiPacket> writeOne)
    {
        using var ms = new System.IO.MemoryStream();
        using (var w = new System.Text.Json.Utf8JsonWriter(ms, MabiJson.WriterOptions(indented: true)))
        {
            if (packets.Count == 1)
            {
                writeOne(w, packets[0]);
            }
            else
            {
                w.WriteStartArray();
                foreach (var p in packets) writeOne(w, p);
                w.WriteEndArray();
            }
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// File-name stem for the selection: <c>packet-0x00005209</c> for one,
    /// <c>packets-0x00005209-x3</c> when all share an op, <c>packets-x3</c> otherwise.
    /// </summary>
    public static string SuggestedStem(IReadOnlyList<MabiPacket> packets)
    {
        if (packets.Count == 0) return "packet";
        if (packets.Count == 1) return $"packet-0x{packets[0].Op:X8}";
        return packets.All(p => p.Op == packets[0].Op)
            ? $"packets-0x{packets[0].Op:X8}-x{packets.Count}"
            : $"packets-x{packets.Count}";
    }
}
