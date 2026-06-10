using System.Globalization;
using Mabipacade.Core.Model;

namespace Mabipacade.DebugUi.ViewModels;

public sealed class FilterViewModel : ObservableObject
{
    private string _opText = string.Empty;
    private string _entityIdText = string.Empty;
    private bool _decodedOnly;
    private HashSet<uint>? _opSet;

    public string OpText
    {
        get => _opText;
        set { if (SetField(ref _opText, value)) RebuildOpSet(); }
    }

    public string EntityIdText
    {
        get => _entityIdText;
        set => SetField(ref _entityIdText, value);
    }

    public bool DecodedOnly
    {
        get => _decodedOnly;
        set => SetField(ref _decodedOnly, value);
    }

    public bool IsAllowed(MabiPacket p)
    {
        if (_opSet is { Count: > 0 } && !_opSet.Contains(p.Op)) return false;
        if (_entityIdText.Length > 0 && !p.EntityId.ToString().Contains(_entityIdText)) return false;
        if (_decodedOnly && p.Decoded is null) return false;
        return true;
    }

    private void RebuildOpSet()
    {
        if (string.IsNullOrWhiteSpace(_opText)) { _opSet = null; return; }
        var set = new HashSet<uint>();
        foreach (var raw in _opText.Split(','))
        {
            var t = raw.Trim();
            if (t.Length == 0) continue;
            if (!t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) { _opSet = null; return; }
            if (!uint.TryParse(t[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var op))
            { _opSet = null; return; }
            set.Add(op);
        }
        _opSet = set;
    }
}
