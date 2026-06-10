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

    private void OnSaveLogAs(object sender, RoutedEventArgs e)
    {
        if (_vm.CurrentLogPath is null)
        {
            MessageBox.Show(this, "No log to save. Start logging first.", "Save Log As",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dlg = new SaveFileDialog
        {
            Filter = "JSON Lines (*.jsonl)|*.jsonl|All files (*.*)|*.*",
            FileName = Path.GetFileName(_vm.CurrentLogPath),
            InitialDirectory = Path.GetDirectoryName(_vm.CurrentLogPath),
        };
        if (dlg.ShowDialog() != true) return;
        try { _vm.SaveLogAs(dlg.FileName); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Save Log As",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnOpenLogsFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_vm.LogsDirectory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _vm.LogsDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Open Logs",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
