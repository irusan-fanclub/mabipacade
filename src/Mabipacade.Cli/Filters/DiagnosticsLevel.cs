using Mabipacade.Core.Diagnostics;

namespace Mabipacade.Cli.Filters;

internal sealed class DiagnosticsLevel
{
    public static readonly DiagnosticsLevel Off = new("off", suppressDiagnostic: true);
    public static readonly DiagnosticsLevel On = new("on", suppressDiagnostic: false);
    public static readonly DiagnosticsLevel Summary = new("summary", suppressDiagnostic: false);

    private readonly bool _suppressDiagnostic;
    public string Name { get; }

    private DiagnosticsLevel(string name, bool suppressDiagnostic)
    {
        Name = name;
        _suppressDiagnostic = suppressDiagnostic;
    }

    public bool PassesLive(SessionEvent ev) => !_suppressDiagnostic || !IsDiagnostic(ev);

    private static bool IsDiagnostic(SessionEvent ev) =>
        ev is SessionEvent.BadBody or SessionEvent.FrameResync or SessionEvent.DecoderFailed;

    public static DiagnosticsLevel Parse(string? spec) => spec?.ToLowerInvariant() switch
    {
        null or "" or "off" => Off,
        "on" => On,
        "summary" => Summary,
        _ => throw new FormatException($"--diagnostics must be off|on|summary, got '{spec}'")
    };

    public override bool Equals(object? obj) => obj is DiagnosticsLevel d && d.Name == Name;
    public override int GetHashCode() => Name.GetHashCode();
}
