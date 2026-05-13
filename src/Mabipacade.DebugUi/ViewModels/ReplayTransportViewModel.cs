using System.Windows.Input;
using Mabipacade.Core.Replay;

namespace Mabipacade.DebugUi.ViewModels;

public sealed class ReplayTransportViewModel : ObservableObject, IDisposable
{
    private readonly ReplayTransport _transport;
    private readonly EventHandler<ReplayState> _stateHandler;
    private readonly EventHandler<TimeSpan> _positionHandler;
    private ReplayState _state;
    private TimeSpan _position;
    private double _rate = 1.0;

    public ReplayState State
    {
        get => _state;
        private set => SetField(ref _state, value);
    }

    public TimeSpan Position
    {
        get => _position;
        private set => SetField(ref _position, value);
    }

    public double Rate
    {
        get => _rate;
        set { if (SetField(ref _rate, value)) _transport.Rate = value; }
    }

    public ICommand PlayCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand StepCommand { get; }

    public ReplayTransportViewModel(ReplayTransport transport)
    {
        _transport = transport;
        _state = transport.State;
        _position = transport.Position;
        _rate = transport.Rate;

        _stateHandler = (_, s) => State = s;
        _positionHandler = (_, p) => Position = p;
        transport.StateChanged += _stateHandler;
        transport.PositionChanged += _positionHandler;

        PlayCommand = new RelayCommand(_ => transport.Play());
        PauseCommand = new RelayCommand(_ => transport.Pause());
        StopCommand = new RelayCommand(_ => transport.Stop());
        StepCommand = new RelayCommand(_ => transport.StepForward());
    }

    public void Dispose()
    {
        _transport.StateChanged -= _stateHandler;
        _transport.PositionChanged -= _positionHandler;
    }
}
