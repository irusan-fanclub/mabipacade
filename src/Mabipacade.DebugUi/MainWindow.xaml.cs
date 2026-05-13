using System.Windows;
using Mabipacade.DebugUi.Services;
using Mabipacade.DebugUi.ViewModels;
using Microsoft.Win32;

namespace Mabipacade.DebugUi;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel(new WpfDispatcher(Dispatcher));
        DataContext = _vm;
        Closing += (_, _) => _vm.Dispose();
    }

    private void OnOpenPcap(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Pcap files (*.pcap)|*.pcap|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dlg.ShowDialog() == true)
            _vm.OpenReplay(dlg.FileName);
    }

    private void OnSelectLive(object sender, RoutedEventArgs e)
    {
        _vm.Source.Mode = SourceMode.Live;
    }
}
