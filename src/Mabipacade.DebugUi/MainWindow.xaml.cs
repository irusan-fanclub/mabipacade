using System.IO;
using System.Linq;
using System.Windows;
using Mabipacade.Core.Model;
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
        _vm.CaptureOutbound = saved.CaptureOutbound;

        DataContext = _vm;
        _vm.LoadNamesFromSettings(saved);

        Closing += (_, _) =>
        {
            _settings.Save(new Models.DebugUiSettings
            {
                WindowWidth = Width,
                WindowHeight = Height,
                LastPcapPath = _vm.Source.OpenPcapPath,
                CaptureOutbound = _vm.CaptureOutbound,
                // Not editable in the UI, so the loaded value is carried through
                // rather than silently dropped by the overwrite.
                XmlDataDirectory = saved.XmlDataDirectory,
            });
            _vm.Dispose();
        };
    }

    private void OnOpenPcap(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Capture files (*.pcap;*.pcapng)|*.pcap;*.pcapng|All files (*.*)|*.*",
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

    /// <summary>
    /// Expanding a 0x5209 in full would build tens of thousands of nodes and
    /// freeze the window, so expansion stops once this many have been opened.
    /// Small payloads open completely; large ones open as far as is useful.
    /// </summary>
    private const int ExpandNodeBudget = 5000;

    private void OnExpandDecoded(object sender, RoutedEventArgs e)
    {
        int budget = ExpandNodeBudget;
        foreach (var node in _vm.Detail.DecodedNodes) Expand(node, ref budget);

        static void Expand(Models.JsonTreeNode node, ref int budget)
        {
            if (budget <= 0 || !node.HasChildren) return;
            node.IsExpanded = true;
            budget--;
            foreach (var child in node.Children)
            {
                if (budget <= 0) return;
                Expand(child, ref budget);
            }
        }
    }

    private void OnCollapseDecoded(object sender, RoutedEventArgs e)
    {
        foreach (var node in _vm.Detail.DecodedNodes) Collapse(node);

        static void Collapse(Models.JsonTreeNode node)
        {
            // Only walk what was built; unexpanded branches have no children yet.
            if (node.ChildrenMaterialised)
                foreach (var child in node.Children) Collapse(child);
            node.IsExpanded = false;
        }
    }

    /// <summary>
    /// The grid allows extended selection, so every export acts on all selected
    /// rows. SelectedItems reports them in click order; walking Items restores
    /// the order the grid displays, which is the order the files should keep.
    /// </summary>
    private List<MabiPacket> SelectedPacketsInDisplayOrder()
    {
        if (PacketGrid.SelectedItems.Count <= 1)
            return PacketGrid.SelectedItem is PacketRowVm one
                ? new List<MabiPacket> { one.Packet } : new List<MabiPacket>();

        var selected = new HashSet<object>(PacketGrid.SelectedItems.Cast<object>());
        return PacketGrid.Items.OfType<PacketRowVm>()
            .Where(selected.Contains).Select(r => r.Packet).ToList();
    }

    private void OnExportPacketJson(object sender, RoutedEventArgs e)
    {
        var packets = SelectedPacketsInDisplayOrder();
        if (packets.Count == 0)
        {
            MessageBox.Show(this, "Select a packet first.", "Export JSON",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json|All files (*.*)|*.*",
            FileName = PacketExportBuilder.SuggestedStem(packets) + ".json",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            // No BOM: the file is meant for jq and editors, both of which are
            // happier without one.
            File.WriteAllText(dlg.FileName, PacketExportBuilder.BuildEnvelopeJson(packets),
                new System.Text.UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Export JSON",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// WPF's DataGrid leaves the selection alone on right-click, so a context
    /// menu would act on whatever was selected before. Selecting the row under
    /// the cursor first makes the menu act on the row that was clicked.
    /// </summary>
    private void OnGridRightClickSelect(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;

        var dep = e.OriginalSource as DependencyObject;
        // A hit on a Run lands in the content tree, which has no visual parent.
        while (dep is System.Windows.FrameworkContentElement fce) dep = fce.Parent;
        while (dep is not null and not System.Windows.Controls.DataGridRow)
            dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);
        // A right-click inside the current multi-selection keeps it, so the
        // context menu can act on all of it; outside, it moves the selection.
        if (dep is System.Windows.Controls.DataGridRow row && !grid.SelectedItems.Contains(row.Item))
            grid.SelectedItem = row.Item;
    }

    /// <summary>
    /// The pcapng-exportable subset of the selection, or null after telling the
    /// user why nothing can be exported. Packets without a raw body (never seen
    /// from the pipeline, only hand-built ones) are dropped with a notice.
    /// </summary>
    private List<MabiPacket>? ExportablePacketsOrExplain(string caption)
    {
        var packets = SelectedPacketsInDisplayOrder();
        if (packets.Count == 0)
        {
            MessageBox.Show(this, "Select a packet first.", caption,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        var exportable = packets.Where(Mabipacade.Core.Recording.PacketPcapExporter.CanExport).ToList();
        if (exportable.Count == 0)
        {
            MessageBox.Show(this, "The selected packets have no raw body to export.", caption,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }
        if (exportable.Count < packets.Count)
        {
            MessageBox.Show(this,
                $"{packets.Count - exportable.Count} of {packets.Count} selected packets have no raw body and will be skipped.",
                caption, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        return exportable;
    }

    private void OnExportPacketPcap(object sender, RoutedEventArgs e)
    {
        if (ExportablePacketsOrExplain("Export pcapng") is not { } packets) return;

        var dlg = new SaveFileDialog
        {
            Filter = "pcapng capture (*.pcapng)|*.pcapng|All files (*.*)|*.*",
            FileName = PacketExportBuilder.SuggestedStem(packets) + ".pcapng",
        };
        if (dlg.ShowDialog() != true) return;

        try { Mabipacade.Core.Recording.PacketPcapExporter.Export(packets, dlg.FileName); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Export pcapng",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// One save dialog, two files: the chosen path gets the pcapng, and the
    /// compact specimen JSON (t/dir/op/opDec/eid/elems) lands beside it with
    /// the same stem.
    /// </summary>
    private void OnExportPacketPcapAndJson(object sender, RoutedEventArgs e)
    {
        if (ExportablePacketsOrExplain("Export pcapng & JSON") is not { } packets) return;

        var dlg = new SaveFileDialog
        {
            Filter = "pcapng capture (*.pcapng)|*.pcapng|All files (*.*)|*.*",
            FileName = PacketExportBuilder.SuggestedStem(packets) + ".pcapng",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            Mabipacade.Core.Recording.PacketPcapExporter.Export(packets, dlg.FileName);
            // The JSON mirrors the pcapng exactly — same packets, same order.
            File.WriteAllText(Path.ChangeExtension(dlg.FileName, ".json"),
                PacketExportBuilder.BuildSlimJson(packets), new System.Text.UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Export pcapng & JSON",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnElemShowInHex(object sender, RoutedEventArgs e)
    {
        if (ElemsGrid.SelectedItem is not Models.ElemTreeNode elem) return;
        if (elem.Offset is not { } offset || elem.Length is not { } length)
        {
            MessageBox.Show(this, "This packet has no raw body, so the elem has no hex location.",
                "Show in Hex", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        int firstLine = _vm.Detail.HighlightHexRange(offset, length);
        if (firstLine < 0) return;

        DetailTabs.SelectedItem = HexTab;
        // The hex grid may not have laid out yet if its tab was never opened;
        // scrolling is deferred until after the tab switch has rendered.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            if (firstLine >= HexGrid.Items.Count) return;
            var item = HexGrid.Items[firstLine];
            HexGrid.ScrollIntoView(item);
            HexGrid.SelectedItem = item;
        });
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
