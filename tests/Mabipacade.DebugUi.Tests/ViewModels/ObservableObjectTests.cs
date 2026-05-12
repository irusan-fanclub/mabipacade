using System.ComponentModel;
using Mabipacade.DebugUi.ViewModels;

namespace Mabipacade.DebugUi.Tests.ViewModels;

public class ObservableObjectTests
{
    private sealed class Sample : ObservableObject
    {
        private int _x;
        public int X { get => _x; set => SetField(ref _x, value); }
    }

    [Fact]
    public void SetField_FiresPropertyChanged_WhenValueChanges()
    {
        var s = new Sample();
        string? changed = null;
        ((INotifyPropertyChanged)s).PropertyChanged += (_, e) => changed = e.PropertyName;
        s.X = 42;
        Assert.Equal(nameof(Sample.X), changed);
    }

    [Fact]
    public void SetField_Suppresses_WhenValueIsSame()
    {
        var s = new Sample();
        s.X = 1;
        int count = 0;
        ((INotifyPropertyChanged)s).PropertyChanged += (_, _) => count++;
        s.X = 1;
        Assert.Equal(0, count);
    }
}
