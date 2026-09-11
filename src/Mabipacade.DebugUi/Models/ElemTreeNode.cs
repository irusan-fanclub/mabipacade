using Mabipacade.Core.Model;
using Mabipacade.Core.Pipeline;

namespace Mabipacade.DebugUi.Models;

public sealed record ElemTreeNode(int Index, string TypeName, string Display,
    int? Offset = null, int? Length = null)
{
    /// <summary>Body offset as shown in the hex pane's Offset column; empty when the packet has no body.</summary>
    public string OffsetLabel => Offset is { } o ? o.ToString("X4") : "";

    public static IEnumerable<ElemTreeNode> From(IReadOnlyList<MessageElem> elems)
        => From(elems, spans: null);

    /// <summary>
    /// Spans come from <see cref="MessageElemReader.TryReadWithSpans"/> over the
    /// same body the elems were parsed from, so index i of both lists describes
    /// the same elem. Null when the packet was built without a body.
    /// </summary>
    public static IEnumerable<ElemTreeNode> From(IReadOnlyList<MessageElem> elems,
        IReadOnlyList<ElemSpan>? spans)
    {
        for (int i = 0; i < elems.Count; i++)
        {
            var e = elems[i];
            string display = e.Type switch
            {
                MessageElemType.Byte   => e.AsByte().ToString(),
                MessageElemType.Short  => e.AsUInt16().ToString(),
                MessageElemType.Int    => e.AsUInt32().ToString(),
                MessageElemType.Long   => e.AsUInt64().ToString(),
                MessageElemType.Float  => e.AsFloat().ToString("R"),
                MessageElemType.String => $"\"{e.AsString()}\"",
                MessageElemType.Bin    => $"<{e.AsBytes().Length} bytes>",
                _                      => "?"
            };
            var span = spans is not null && i < spans.Count ? spans[i] : (ElemSpan?)null;
            yield return new ElemTreeNode(i, e.Type.ToString(), display,
                span?.Offset, span?.Length);
        }
    }
}
