using System.Collections.ObjectModel;
using Mabipacade.Core.Model;

namespace Mabipacade.DebugUi.ViewModels;

public sealed class PacketListViewModel : ObservableObject
{
    private readonly FilterViewModel _filter;
    private readonly int _maxRows;

    public ObservableCollection<PacketRowVm> Rows { get; } = new();

    public PacketListViewModel(FilterViewModel filter, int maxRows = 50_000)
    {
        _filter = filter;
        _maxRows = maxRows;
    }

    public void AddPacket(MabiPacket p)
    {
        if (!_filter.IsAllowed(p)) return;
        Rows.Add(new PacketRowVm(p));
        while (Rows.Count > _maxRows) Rows.RemoveAt(0);
    }

    public void Clear() => Rows.Clear();
}
