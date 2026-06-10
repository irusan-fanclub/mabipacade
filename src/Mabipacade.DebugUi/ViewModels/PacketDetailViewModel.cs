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

    public void SetNameResolver(Mabipacade.DebugUi.Resolution.NameResolver names)
    {
        _names = names;
        Rebuild();
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private void Rebuild()
    {
        HexLines.Clear();
        ElemNodes.Clear();
        NameLookups.Clear();

        if (_selectedRow is null) { DecodedJson = "no packet selected"; return; }

        var packet = _selectedRow.Packet;
        DecodedJson = packet.Decoded is null
            ? $"no decoder registered for op 0x{packet.Op:X8}"
            : JsonSerializer.Serialize(packet.Decoded, packet.Decoded.GetType(), JsonOpts);

        foreach (var n in ElemTreeNode.From(packet.Elems)) ElemNodes.Add(n);

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
