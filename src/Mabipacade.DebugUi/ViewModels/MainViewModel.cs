using System.IO;
using System.Windows.Input;
using Mabipacade.Core.Diagnostics;
using Mabipacade.DebugUi.Services;

namespace Mabipacade.DebugUi.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly IUiDispatcher _dispatcher;
    private PipelineHost? _activeHost;
    private ReplaySession? _replaySession;
    private LiveSession? _liveSession;

    public SourceViewModel Source { get; } = new();
    public FilterViewModel Filter { get; } = new();
    public PacketListViewModel PacketList { get; }
    public PacketDetailViewModel Detail { get; } = new();
    public StatusViewModel Status { get; } = new();
    public ReplayTransportViewModel? ReplayTransport { get; private set; }

    public ICommand OpenReplayCommand { get; }
    public ICommand StartLiveCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand ClearCommand { get; }

    public MainViewModel(IUiDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        PacketList = new PacketListViewModel(Filter);

        OpenReplayCommand = new RelayCommand(p => OpenReplay(p as string ?? Source.OpenPcapPath ?? ""));
        StartLiveCommand = new RelayCommand(_ => StartLive());
        StopCommand = new RelayCommand(_ => StopActive());
        ClearCommand = new RelayCommand(_ =>
        {
            PacketList.Clear();
            Detail.SelectedRow = null;
        });
    }

    public void OpenReplay(string pcapPath)
    {
        if (string.IsNullOrWhiteSpace(pcapPath) || !File.Exists(pcapPath)) return;
        StopActive();
        var session = ReplaySessionFactory.Create(pcapPath, _dispatcher);
        _replaySession = session;
        _activeHost = session.Host;
        ReplayTransport = new ReplayTransportViewModel(session.Transport);
        OnPropertyChanged(nameof(ReplayTransport));

        Wire(session.Host);
        Source.Mode = SourceMode.Replay;
        Source.OpenPcapPath = pcapPath;
        _ = session.Host.StartAsync(CancellationToken.None);
    }

    public void StartLive()
    {
        StopActive();
        try
        {
            var session = LiveSessionFactory.Create("tw", "Client.exe", _dispatcher);
            _liveSession = session;
            _activeHost = session.Host;
            Wire(session.Host);
            Source.Mode = SourceMode.Live;
            _ = session.Host.StartAsync(CancellationToken.None);
        }
        catch (LiveSessionFactory.BootstrapException e)
        {
            Status.HandleEvent(new SessionEvent.SessionEnd(DateTime.UtcNow, "bootstrap: " + e.Message));
        }
    }

    public void StopActive()
    {
        if (_activeHost is null) return;
        _ = _activeHost.StopAsync();
        _activeHost.Dispose();
        _activeHost = null;
        _replaySession = null;
        _liveSession = null;
        ReplayTransport = null;
        OnPropertyChanged(nameof(ReplayTransport));
    }

    private void Wire(PipelineHost host)
    {
        host.PacketReceived += (_, p) => PacketList.AddPacket(p);
        host.SessionEventReceived += (_, e) => Status.HandleEvent(e);
    }

    public void Dispose() => StopActive();
}
