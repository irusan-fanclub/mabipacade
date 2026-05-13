using System.Windows.Input;
using Mabipacade.Core.Replay;

namespace Mabipacade.DebugUi.ViewModels;

public sealed class ReplayTransportViewModel : ObservableObject
{
    private readonly ReplayTransport _transport;
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

        transport.StateChanged += (_, s) => State = s;
        transport.PositionChanged += (_, p) => Position = p;

        PlayCommand = new RelayCommand(_ => transport.Play());
        PauseCommand = new RelayCommand(_ => transport.Pause());
        StopCommand = new RelayCommand(_ => transport.Stop());
        StepCommand = new RelayCommand(_ => transport.StepForward());
    }
}
