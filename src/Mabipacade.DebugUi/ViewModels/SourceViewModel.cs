namespace Mabipacade.DebugUi.ViewModels;

public enum SourceMode { Live, Replay }

public sealed class SourceViewModel : ObservableObject
{
    private SourceMode _mode = SourceMode.Live;
    private string? _openPcapPath;

    public SourceMode Mode
    {
        get => _mode;
        set => SetField(ref _mode, value);
    }

    public string? OpenPcapPath
    {
        get => _openPcapPath;
        set => SetField(ref _openPcapPath, value);
    }
}
