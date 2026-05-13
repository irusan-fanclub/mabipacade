using Mabipacade.Core.Model;

namespace Mabipacade.DebugUi.Models;

public sealed record ElemTreeNode(int Index, string TypeName, string Display)
{
    public static IEnumerable<ElemTreeNode> From(IReadOnlyList<MessageElem> elems)
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
            yield return new ElemTreeNode(i, e.Type.ToString(), display);
        }
    }
}
