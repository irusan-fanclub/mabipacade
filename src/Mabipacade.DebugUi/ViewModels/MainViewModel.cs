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

    private System.Timers.Timer? _statsTimer;
    private long _lastFrames;
    private long _lastPackets;

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

        _statsTimer = new System.Timers.Timer(1000) { AutoReset = true };
        _statsTimer.Elapsed += OnStatsTick;
        _statsTimer.Start();
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
        Status.SetConnection($"replay: {Path.GetFileName(pcapPath)}");
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
            Status.SetConnection($"live {session.Endpoint.RemoteAddress}:{session.Endpoint.RemotePort}");
            IsRunning = true;
        }
        catch (LiveSessionFactory.BootstrapException e)
        {
            Status.SetConnection("bootstrap failed: " + e.Message);
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
        ReplayTransport?.Dispose();
        ReplayTransport = null;
        OnPropertyChanged(nameof(ReplayTransport));
        ActivityState = "○ Stopped";
        Status.SetConnection("disconnected");
        IsRunning = false;
        _lastFrames = 0;
        _lastPackets = 0;
        Status.UpdateCounters(0, 0, 0, 0);
    }

    private void OnStatsTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        var host = _activeHost;
        if (host is null) return;
        long packets = host.Metrics.TotalPackets;
        long frames = host.Metrics.TotalFrames;
        long pps = packets - _lastPackets;
        long fps = frames - _lastFrames;
        _lastPackets = packets;
        _lastFrames = frames;
        _dispatcher.BeginInvoke(() => Status.UpdateCounters(
            totalPackets: packets,
            badBody: host.Metrics.BadBodyCount,
            framesPerSec: pps,
            bytesPerSec: fps * 256));
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

    public void Dispose()
    {
        _statsTimer?.Stop();
        _statsTimer?.Dispose();
        StopActive();
    }
}
