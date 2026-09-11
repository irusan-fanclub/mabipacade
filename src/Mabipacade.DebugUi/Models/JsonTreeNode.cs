using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using Mabipacade.DebugUi.ViewModels;

namespace Mabipacade.DebugUi.Models;

/// <summary>
/// One row of the collapsible view over a decoded payload.
///
/// Children are built only when the node is expanded. A 0x5209 decodes to 804
/// items and 116 quests, each several levels deep — building that whole tree on
/// selection would stall the UI, and almost all of it is never looked at.
/// </summary>
public sealed class JsonTreeNode : ObservableObject
{
    /// <summary>Keeps a tree row to one line; the full text stays in <see cref="Value"/>.</summary>
    private const int SummaryValueLimit = 120;

    private readonly JsonNode? _node;
    private bool _isExpanded;

    private JsonTreeNode(string name, JsonNode? node)
    {
        Name = name;
        _node = node;
        Children = new ObservableCollection<JsonTreeNode>();
    }

    public string Name { get; }

    public ObservableCollection<JsonTreeNode> Children { get; }

    /// <summary>True once <see cref="Children"/> holds the real children rather than nothing.</summary>
    public bool ChildrenMaterialised { get; private set; }

    public bool HasChildren => _node is JsonObject o ? o.Count > 0
        : _node is JsonArray a && a.Count > 0;

    /// <summary>
    /// The leaf's value untruncated and unquoted — what you want when copying a
    /// quest description or an attribute run out of the tree. Empty for objects
    /// and arrays.
    /// </summary>
    public string Value
    {
        get
        {
            if (_node is JsonObject or JsonArray) return "";
            if (_node is null) return "null";
            var value = _node.AsValue();
            return value.TryGetValue<string>(out var s) ? s : value.ToJsonString();
        }
    }

    public string Summary => _node switch
    {
        JsonObject o => $"{Name}  {{{o.Count}}}",
        JsonArray a => $"{Name}  [{a.Count}]",
        _ => $"{Name}: {Truncate(Render(_node))}",
    };

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (!SetField(ref _isExpanded, value)) return;
            if (value) Materialise();
        }
    }

    /// <summary>
    /// Builds a tree from a JSON document. Invalid JSON yields a single node
    /// carrying the parse error rather than throwing — the detail pane is a
    /// debugging aid and must render something for every packet.
    /// </summary>
    public static JsonTreeNode FromJson(string json, string rootName)
    {
        try
        {
            return new JsonTreeNode(rootName, JsonNode.Parse(json));
        }
        catch (System.Text.Json.JsonException ex)
        {
            return new JsonTreeNode($"{rootName} (unparseable: {ex.Message})", null);
        }
    }

    private void Materialise()
    {
        if (ChildrenMaterialised) return;
        ChildrenMaterialised = true;

        switch (_node)
        {
            case JsonObject o:
                foreach (var kv in o) Children.Add(new JsonTreeNode(kv.Key, kv.Value));
                break;
            case JsonArray a:
                for (int i = 0; i < a.Count; i++) Children.Add(new JsonTreeNode($"[{i}]", a[i]));
                break;
        }
    }

    private static string Render(JsonNode? node)
    {
        if (node is null) return "null";
        var value = node.AsValue();
        // Strings are quoted so an empty one is visible as "" rather than blank.
        return value.TryGetValue<string>(out var s) ? $"\"{s}\"" : value.ToJsonString();
    }

    private static string Truncate(string s) =>
        s.Length <= SummaryValueLimit ? s : string.Concat(s.AsSpan(0, SummaryValueLimit), "…");
}
