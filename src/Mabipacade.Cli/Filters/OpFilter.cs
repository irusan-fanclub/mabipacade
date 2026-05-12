using System.Globalization;

namespace Mabipacade.Cli.Filters;

internal sealed class OpFilter
{
    private readonly HashSet<ushort>? _allowed;

    private OpFilter(HashSet<ushort>? allowed) { _allowed = allowed; }

    public static OpFilter Parse(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return new OpFilter(null);
        var set = new HashSet<ushort>();
        foreach (var raw in spec.Split(','))
        {
            var token = raw.Trim();
            if (token.Length == 0) continue;
            if (!token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                throw new FormatException($"Op '{token}' must be hex like 0xXXXX");
            var hex = token[2..];
            if (!ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var op))
                throw new FormatException($"Op '{token}' is not a valid hex ushort");
            set.Add(op);
        }
        return new OpFilter(set.Count == 0 ? null : set);
    }

    public bool Allows(ushort op) => _allowed is null || _allowed.Contains(op);
}
