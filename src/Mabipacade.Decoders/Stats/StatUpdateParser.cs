using Mabipacade.Core.Model;

namespace Mabipacade.Decoders.Stats;

/// <summary>
/// Shared parser for StatUpdate packets (0x7530 private / 0x7532 public).
/// Layout: [Byte count], then count× (Int statId, typed value).
/// The value elem type varies (Float/Int/Short/Byte/Long); it is converted to
/// double generically. Parsing stops at the first structural mismatch so that a
/// well-formed prefix is still captured from irregular payloads.
/// </summary>
internal static class StatUpdateParser
{
    public static IReadOnlyList<(uint StatId, double Value)> Parse(IReadOnlyList<MessageElem> elems)
    {
        var result = new List<(uint StatId, double Value)>();
        if (elems.Count == 0 || elems[0].Type != MessageElemType.Byte)
            return result;

        int count = elems[0].AsByte();
        int i = 1;
        for (int n = 0; n < count; n++)
        {
            // Need a statId (Int) followed by one value elem.
            if (i + 1 >= elems.Count) break;
            if (elems[i].Type != MessageElemType.Int) break;

            uint statId = elems[i].AsUInt32();
            if (!TryToDouble(elems[i + 1], out double value)) break;

            result.Add((statId, value));
            i += 2;
        }

        return result;
    }

    private static bool TryToDouble(MessageElem e, out double value)
    {
        switch (e.Type)
        {
            case MessageElemType.Float: value = e.AsFloat(); return true;
            case MessageElemType.Int:   value = e.AsUInt32(); return true;
            case MessageElemType.Short: value = e.AsUInt16(); return true;
            case MessageElemType.Byte:  value = e.AsByte();   return true;
            case MessageElemType.Long:  value = e.AsUInt64(); return true;
            default: value = 0; return false;
        }
    }
}
