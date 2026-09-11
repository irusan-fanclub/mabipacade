namespace Mabipacade.DebugUi.Models;

public sealed class DebugUiSettings
{
    public double WindowWidth { get; set; } = 1000;
    public double WindowHeight { get; set; } = 600;
    public string? LastPcapPath { get; set; }
    public string? XmlDataDirectory { get; set; }

    /// <summary>Capture client→server frames too. Off by default: inbound-only, the original behaviour.</summary>
    public bool CaptureOutbound { get; set; }
}
