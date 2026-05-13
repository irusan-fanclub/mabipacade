using System.IO;
using System.Windows;
using Mabipacade.DebugUi.Services;
using Mabipacade.DebugUi.ViewModels;
using Microsoft.Win32;

namespace Mabipacade.DebugUi;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly SettingsService _settings;

    public MainWindow()
    {
        InitializeComponent();
        _settings = new SettingsService(AppContext.BaseDirectory);
        var saved = _settings.Load();
        Width = saved.WindowWidth;
        Height = saved.WindowHeight;

        _vm = new MainViewModel(new WpfDispatcher(Dispatcher));
        if (saved.LastPcapPath is { } path && File.Exists(path))
            _vm.Source.OpenPcapPath = path;

        DataContext = _vm;
        _vm.LoadNamesFromSettings(saved);

        Closing += (_, _) =>
        {
            _settings.Save(new Models.DebugUiSettings
            {
                WindowWidth = Width,
                WindowHeight = Height,
                LastPcapPath = _vm.Source.OpenPcapPath,
            });
            _vm.Dispose();
        };
    }

    private void OnOpenPcap(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Pcap files (*.pcap)|*.pcap|All files (*.*)|*.*",
            CheckFileExists = true,
            InitialDirectory = _vm.Source.OpenPcapPath is { } p
                ? Path.GetDirectoryName(p)
                : null,
        };
        if (dlg.ShowDialog() == true)
            _vm.OpenReplay(dlg.FileName);
    }

    private void OnSelectLive(object sender, RoutedEventArgs e)
    {
        _vm.Source.Mode = SourceMode.Live;
    }
}
