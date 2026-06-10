namespace Mabipacade.Decoders;

public static class OpCodeNames
{
    private static readonly Dictionary<uint, string> _names = Enum
        .GetValues<OpCodes>()
        .ToDictionary(o => (uint)o, o => o.ToString());

    public static string? TryGetName(uint op) =>
        _names.TryGetValue(op, out var name) ? name : null;
}
