using Mabipacade.Core.Diagnostics;

namespace Mabipacade.DebugUi.ViewModels;

public sealed class StatusViewModel : ObservableObject
{
    private string _connectionLabel = "disconnected";
    private long _totalPackets;
    private long _badBodyCount;
    private long _packetsPerSec;
    private long _bytesPerSec;

    public string ConnectionLabel { get => _connectionLabel; private set => SetField(ref _connectionLabel, value); }
    public long TotalPackets { get => _totalPackets; private set => SetField(ref _totalPackets, value); }
    public long BadBodyCount { get => _badBodyCount; private set => SetField(ref _badBodyCount, value); }
    public long PacketsPerSec { get => _packetsPerSec; private set => SetField(ref _packetsPerSec, value); }
    public long BytesPerSec { get => _bytesPerSec; private set => SetField(ref _bytesPerSec, value); }

    public void HandleEvent(SessionEvent ev)
    {
        ConnectionLabel = ev switch
        {
            SessionEvent.ConnectionEstablished c => $"connected {c.Remote.Address}:{c.Remote.Port}",
            SessionEvent.ConnectionLost c => $"lost {c.LastRemote.Address}:{c.LastRemote.Port}",
            SessionEvent.ConnectionResumed c => $"resumed {c.NewRemote.Address}:{c.NewRemote.Port}",
            SessionEvent.SessionEnd => "disconnected",
            _ => ConnectionLabel
        };
    }

    public void UpdateCounters(long totalPackets, long badBody, long framesPerSec, long bytesPerSec)
    {
        TotalPackets = totalPackets;
        BadBodyCount = badBody;
        PacketsPerSec = framesPerSec;
        BytesPerSec = bytesPerSec;
    }
}
