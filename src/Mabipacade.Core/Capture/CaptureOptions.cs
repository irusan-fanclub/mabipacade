namespace Mabipacade.Core.Capture;

public sealed class CaptureOptions
{
    public string[] ProcessNames { get; init; } = new[] { "Client.exe" };
    public RegionProfile? Region { get; init; }
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);
    public bool PreferProcessDetection { get; init; } = true;
}
