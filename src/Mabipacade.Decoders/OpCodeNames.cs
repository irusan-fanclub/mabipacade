namespace Mabipacade.Decoders;

public static class OpCodeNames
{
    private static readonly Dictionary<ushort, string> _names = Enum
        .GetValues<OpCodes>()
        .ToDictionary(o => (ushort)o, o => o.ToString());

    public static string? TryGetName(ushort op) =>
        _names.TryGetValue(op, out var name) ? name : null;
}
