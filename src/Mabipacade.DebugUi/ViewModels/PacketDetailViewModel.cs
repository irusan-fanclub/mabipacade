using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using Mabipacade.DebugUi.Models;

namespace Mabipacade.DebugUi.ViewModels;

public sealed class PacketDetailViewModel : ObservableObject
{
    private PacketRowVm? _selectedRow;
    private string _decodedJson = "no packet selected";
    private Mabipacade.DebugUi.Resolution.NameResolver _names = Mabipacade.DebugUi.Resolution.NameResolver.Empty;

    public PacketRowVm? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (!SetField(ref _selectedRow, value)) return;
            Rebuild();
        }
    }

    public string DecodedJson
    {
        get => _decodedJson;
        private set => SetField(ref _decodedJson, value);
    }

    public ObservableCollection<HexDumpLine> HexLines { get; } = new();
    public ObservableCollection<ElemTreeNode> ElemNodes { get; } = new();
    public ObservableCollection<NameLookup> NameLookups { get; } = new();

    /// <summary>
    /// Collapsible view of the decoded payload. Holds a single root so the
    /// TreeView has something to bind to; 0x5209 decodes far too deep to read as
    /// flat text.
    /// </summary>
    public ObservableCollection<JsonTreeNode> DecodedNodes { get; } = new();

    public void SetNameResolver(Mabipacade.DebugUi.Resolution.NameResolver names)
    {
        _names = names;
        Rebuild();
    }

    /// <summary>
    /// The selected packet as a full indented JSON envelope — the same shape the
    /// CLI emits — for opening in an external viewer. Null when nothing is
    /// selected.
    /// </summary>
    public string? BuildPacketJson()
    {
        if (_selectedRow is null) return null;

        using var ms = new System.IO.MemoryStream();
        using (var w = new System.Text.Json.Utf8JsonWriter(
                   ms, Mabipacade.Core.Json.MabiJson.WriterOptions(indented: true)))
        {
            Mabipacade.Core.Json.EnvelopeShape.WritePacket(
                w, _selectedRow.Packet, Mabipacade.Decoders.OpCodeNames.TryGetName);
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>Default file name for the export, e.g. <c>packet-0x00005209.json</c>.</summary>
    public string SuggestedExportFileName =>
        _selectedRow is null ? "packet.json" : $"packet-0x{_selectedRow.Packet.Op:X8}.json";

    /// <summary>
    /// The selected packet in the compact specimen shape (t / dir / op / opDec /
    /// eid / elems) that sits next to a single-packet pcapng. Null when nothing
    /// is selected.
    /// </summary>
    public string? BuildPacketSlimJson()
    {
        if (_selectedRow is null) return null;

        using var ms = new System.IO.MemoryStream();
        using (var w = new System.Text.Json.Utf8JsonWriter(
                   ms, Mabipacade.Core.Json.MabiJson.WriterOptions(indented: true)))
        {
            Mabipacade.Core.Json.EnvelopeShape.WritePacketSlim(w, _selectedRow.Packet);
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>Default file name for the pcapng export, e.g. <c>packet-0x00005209.pcapng</c>.</summary>
    public string SuggestedPcapExportFileName =>
        _selectedRow is null ? "packet.pcapng" : $"packet-0x{_selectedRow.Packet.Op:X8}.pcapng";

    /// <summary>False when nothing is selected or the packet carries no raw body to re-frame.</summary>
    public bool CanExportPcap =>
        _selectedRow is not null && Mabipacade.Core.Recording.PacketPcapExporter.CanExport(_selectedRow.Packet);

    /// <summary>Writes the selected packet as a one-frame pcapng. No-op without a selection.</summary>
    public void ExportPacketPcap(string path)
    {
        if (_selectedRow is null) return;
        Mabipacade.Core.Recording.PacketPcapExporter.Export(_selectedRow.Packet, path);
    }

    /// <summary>
    /// Repaints the hex pane with the byte range [offset, +length) of the body
    /// highlighted. Returns the index of the first hex line the range touches —
    /// what the view scrolls to — or -1 when there is no body to highlight in.
    /// </summary>
    public int HighlightHexRange(int offset, int length)
    {
        if (_selectedRow?.Packet.Body is not { Length: > 0 } body) return -1;

        HexLines.Clear();
        int firstHit = -1, i = 0;
        foreach (var line in HexDumpLine.From(body, offset, length))
        {
            if (firstHit < 0 && line.HasHit) firstHit = i;
            HexLines.Add(line);
            i++;
        }
        return firstHit;
    }

    private static readonly JsonSerializerOptions JsonOpts =
        Mabipacade.Core.Json.MabiJson.SerializerOptions(indented: true);

    private void Rebuild()
    {
        HexLines.Clear();
        ElemNodes.Clear();
        NameLookups.Clear();
        DecodedNodes.Clear();

        if (_selectedRow is null) { DecodedJson = "no packet selected"; return; }

        var packet = _selectedRow.Packet;
        DecodedJson = packet.Decoded is null
            ? $"no decoder registered for op 0x{packet.Op:X8}"
            : JsonSerializer.Serialize(packet.Decoded, packet.Decoded.GetType(), JsonOpts);

        if (packet.Decoded is not null)
        {
            var root = JsonTreeNode.FromJson(DecodedJson, packet.Decoded.GetType().Name);
            // Open the first level so the top-level shape is visible without a
            // click; everything below stays collapsed and unbuilt.
            root.IsExpanded = true;
            DecodedNodes.Add(root);
        }

        // Re-walking the body recovers each elem's byte range, which is what
        // lets an elem row point back into the hex pane.
        System.Collections.Generic.IReadOnlyList<Core.Pipeline.ElemSpan>? spans = null;
        if (packet.Body is { Length: > 0 } elemSource &&
            Core.Pipeline.MessageElemReader.TryReadWithSpans(elemSource, out _, out var walked)
                == Core.Pipeline.ReadElemsResult.Ok)
        {
            spans = walked;
        }
        foreach (var n in ElemTreeNode.From(packet.Elems, spans)) ElemNodes.Add(n);

        // Hex dump: prefer the raw message body (everything after op + entityId).
        // Falls back to Bin elem bytes for packets constructed directly without a body.
        if (packet.Body is { Length: > 0 } body)
        {
            foreach (var line in HexDumpLine.From(body)) HexLines.Add(line);
        }
        else
        {
            foreach (var e in packet.Elems)
            {
                if (e.Type != Core.Model.MessageElemType.Bin) continue;
                foreach (var line in HexDumpLine.From(e.AsBytes())) HexLines.Add(line);
            }
        }

        var ids = Mabipacade.DebugUi.Resolution.DecodedSkillExtractor.ExtractSkillIds(packet.Decoded);
        foreach (var id in ids.Distinct())
        {
            if (_names.TryResolveSkillFull(id, out var entry))
                NameLookups.Add(new NameLookup(entry.SkillId, entry.EnglishName, entry.LocalName));
            else
                NameLookups.Add(new NameLookup(id, "?", "?"));
        }
    }
}
