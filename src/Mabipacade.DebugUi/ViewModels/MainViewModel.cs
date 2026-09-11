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
    private readonly PacketLogger _logger = new();
    private readonly FrameRecorder _frames = new();

    // Held so the subscription can be undone after StopActive has cleared the
    // session it came from.
    private Mabipacade.Core.Sources.IFrameSource? _recordingSource;
    private EventHandler<Mabipacade.Core.Sources.RawFrameEventArgs>? _frameHandler;
    private CancellationTokenSource? _watchdogCts;
    private string _logsDirectory;
    private bool _isRunning;
    private bool _captureOutbound;
    private string _activityState = "○ Stopped";
    private string _loggerLabel = "log: off";

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

    /// <summary>
    /// Whether the next live capture also takes client→server frames. Read at
    /// Start Live; toggling it mid-capture changes nothing until a restart.
    /// </summary>
    public bool CaptureOutbound
    {
        get => _captureOutbound;
        set => SetField(ref _captureOutbound, value);
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
    public ICommand StartLogCommand { get; }
    public ICommand StopLogCommand { get; }

    public bool IsLogging => _logger.IsActive;
    public string? CurrentLogPath => _logger.CurrentPath;

    /// <summary>The pcapng being written, or null when only the NDJSON log is running.</summary>
    public string? CurrentCapturePath => _frames.IsActive ? _frames.CurrentPath : null;

    /// <summary>Frames dropped because the writer fell behind; non-zero means the capture has gaps.</summary>
    public long FramesDropped => _frames.FramesDropped;
    public string LoggerLabel
    {
        get => _loggerLabel;
        private set => SetField(ref _loggerLabel, value);
    }
    public string LogsDirectory
    {
        get => _logsDirectory;
        set => SetField(ref _logsDirectory, value);
    }

    public MainViewModel(IUiDispatcher dispatcher)
        : this(dispatcher, Path.Combine(AppContext.BaseDirectory, "logs")) { }

    public MainViewModel(IUiDispatcher dispatcher, string logsDirectory)
    {
        _dispatcher = dispatcher;
        _logsDirectory = logsDirectory;
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
        StartLogCommand = new RelayCommand(_ => StartLogging(), _ => !_logger.IsActive);
        StopLogCommand = new RelayCommand(_ => StopLogging(), _ => _logger.IsActive);

        _logger.StateChanged += (_, _) => _dispatcher.BeginInvoke(OnLoggerStateChanged);

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
            BeginLiveSession(LiveSessionFactory.Create("tw", "Client.exe", _dispatcher,
                captureOutbound: CaptureOutbound));
        }
        catch (LiveSessionFactory.BootstrapException e)
        {
            Status.SetConnection("bootstrap failed: " + e.Message);
            ActivityState = "○ Stopped (bootstrap failed)";
            IsRunning = false;
        }
    }

    /// <summary>
    /// Takes over a live capture and starts everything that hangs off it.
    /// Separate from <see cref="StartLive"/> so the wiring can be exercised
    /// without a running game.
    /// </summary>
    internal void BeginLiveSession(LiveSession session)
    {
        _liveSession = session;
        _activeHost = session.Host;
        Wire(session.Host);
        Source.Mode = SourceMode.Live;
        _ = session.Host.StartAsync(CancellationToken.None);

        // Follows the client's connections: keeps the capture filter current
        // across a channel switch, and reports the switch as a session event.
        _watchdogCts = new CancellationTokenSource();
        session.Watchdog.SessionEventReceived += (_, e) => _dispatcher.BeginInvoke(() => Status.HandleEvent(e));
        _ = session.Watchdog.RunAsync(_watchdogCts.Token);

        // A log may already be running — started before the capture was, or
        // carried over from a session that just ended. Either way the frames
        // belong in it.
        SyncFrameRecording();

        ActivityState = "● Live";
        Status.SetConnection($"live {session.Endpoint.RemoteAddress}:{session.Endpoint.RemotePort}");
        IsRunning = true;
    }

    public void StopActive()
    {
        if (_activeHost is null) return;
        // The frames being recorded come from this session, so the recording
        // ends with it rather than being left holding a dead source.
        StopFrameRecording();
        _watchdogCts?.Cancel();
        _watchdogCts?.Dispose();
        _watchdogCts = null;
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
        host.PacketReceived += (_, p) =>
        {
            PacketList.AddPacket(p);
            if (_logger.IsActive) _logger.Append(p);
        };
        host.SessionEventReceived += (_, e) => Status.HandleEvent(e);
    }

    /// <summary>
    /// Starts a recording session: decoded packets as NDJSON, and — when the
    /// source is live — the raw frames as pcapng alongside it, sharing the
    /// file stem.
    /// </summary>
    public string StartLogging(string? path = null)
    {
        if (_logger.IsActive) return _logger.CurrentPath!;
        var target = path ?? PacketLogger.BuildDefaultPath(_logsDirectory, DateTime.Now);
        _logger.Start(target);
        SyncFrameRecording();
        return target;
    }

    public void StopLogging()
    {
        StopFrameRecording();
        _logger.Stop();
    }

    /// <summary>
    /// Brings frame recording in line with the current state instead of arming
    /// it at one moment: it runs exactly while a log and a live capture are both
    /// going. Arming once was the bug — starting the log before the capture, or
    /// restarting the capture under a running log, left the pcapng missing while
    /// the NDJSON filled up as usual.
    /// </summary>
    private void SyncFrameRecording()
    {
        bool shouldRecord = _logger.IsActive && _liveSession is not null;
        if (shouldRecord == _frames.IsActive) return;

        if (shouldRecord) StartFrameRecording(_logger.CurrentPath!);
        else StopFrameRecording();
    }

    /// <summary>
    /// Only a live capture is worth recording to pcapng. In replay the frames
    /// came from a capture file that already exists, so recording them would
    /// just copy it.
    /// </summary>
    private void StartFrameRecording(string logPath)
    {
        if (_liveSession is not { } live) return;

        var capturePath = Path.ChangeExtension(logPath, ".pcapng");
        try
        {
            _frames.Start(capturePath, PacketDotNet.LinkLayers.Ethernet,
                nicDescription: live.NicDescription,
                captureFilter: live.CaptureFilter);
        }
        catch (IOException)
        {
            // The NDJSON log is already running; losing the capture file is
            // worth reporting but not worth aborting the session over.
            Status.SetConnection($"capture file unavailable: {Path.GetFileName(capturePath)}");
            return;
        }

        _recordingSource = live.Source;
        _frameHandler = (_, e) => _frames.Append(e.Data, e.TimestampUtc);
        _recordingSource.FrameReceived += _frameHandler;
    }

    private void StopFrameRecording()
    {
        if (_recordingSource is not null && _frameHandler is not null)
            _recordingSource.FrameReceived -= _frameHandler;
        _recordingSource = null;
        _frameHandler = null;
        _frames.Stop();
    }

    public void SaveLogAs(string targetPath) => _logger.CopyTo(targetPath);

    private void OnLoggerStateChanged()
    {
        OnPropertyChanged(nameof(IsLogging));
        OnPropertyChanged(nameof(CurrentLogPath));
        (StartLogCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (StopLogCommand as RelayCommand)?.RaiseCanExecuteChanged();
        // The "+pcapng" tail is how you can tell at a glance whether the raw
        // capture is being written too — it is not, in replay.
        LoggerLabel = _logger.IsActive
            ? $"log: {Path.GetFileName(_logger.CurrentPath)}{(_frames.IsActive ? " +pcapng" : "")}"
            : _logger.CurrentPath is { } last
                ? $"log: {Path.GetFileName(last)} (stopped)"
                : "log: off";
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
        _logger.Dispose();
        _frames.Dispose();
    }
}
