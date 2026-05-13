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

    private readonly Services.NameResolverService _nameResolver = new();
    private bool _isRunning;
    private string _activityState = "○ Stopped";

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (!SetField(ref _isRunning, value)) return;
            (StartLiveCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (StopCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (OpenReplayCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string ActivityState
    {
        get => _activityState;
        private set => SetField(ref _activityState, value);
    }

    public SourceViewModel Source { get; } = new();
    public FilterViewModel Filter { get; } = new();
    public PacketListViewModel PacketList { get; }
    public PacketDetailViewModel Detail { get; } = new();
    public StatusViewModel Status { get; } = new();
    public ReplayTransportViewModel? ReplayTransport { get; private set; }

    public Services.NameResolverService NameResolverService => _nameResolver;

    public ICommand OpenReplayCommand { get; }
    public ICommand StartLiveCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand ClearCommand { get; }

    public MainViewModel(IUiDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        PacketList = new PacketListViewModel(Filter);
        _nameResolver.Changed += (_, _) =>
        {
            PacketList.SetNameResolver(_nameResolver.Current);
            Detail.SetNameResolver(_nameResolver.Current);
        };

        OpenReplayCommand = new RelayCommand(p => OpenReplay(p as string ?? Source.OpenPcapPath ?? ""), _ => !_isRunning);
        StartLiveCommand = new RelayCommand(_ => StartLive(), _ => !_isRunning);
        StopCommand = new RelayCommand(_ => StopActive(), _ => _isRunning);
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
        ReplayTransport.Rate = 10000.0;
        OnPropertyChanged(nameof(ReplayTransport));

        Wire(session.Host);
        Source.Mode = SourceMode.Replay;
        Source.OpenPcapPath = pcapPath;
        _ = session.Host.StartAsync(CancellationToken.None);
        ActivityState = "▶ Replay";
        IsRunning = true;
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
            ActivityState = "● Live";
            IsRunning = true;
        }
        catch (LiveSessionFactory.BootstrapException e)
        {
            Status.HandleEvent(new SessionEvent.SessionEnd(DateTime.UtcNow, "bootstrap: " + e.Message));
            ActivityState = "○ Stopped (bootstrap failed)";
            IsRunning = false;
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
        ActivityState = "○ Stopped";
        IsRunning = false;
    }

    private void Wire(PipelineHost host)
    {
        host.PacketReceived += (_, p) => PacketList.AddPacket(p);
        host.SessionEventReceived += (_, e) => Status.HandleEvent(e);
    }

    public void LoadNamesFromSettings(Models.DebugUiSettings settings)
    {
        _nameResolver.Reload(settings);
    }

    public void Dispose() => StopActive();
}
