using System.Collections.ObjectModel;
using Mabipacade.Core.Model;
using Mabipacade.DebugUi.Resolution;

namespace Mabipacade.DebugUi.ViewModels;

public sealed class PacketListViewModel : ObservableObject
{
    private readonly FilterViewModel _filter;
    private readonly int _maxRows;
    private NameResolver _names = NameResolver.Empty;

    public ObservableCollection<PacketRowVm> Rows { get; } = new();

    public PacketListViewModel(FilterViewModel filter, int maxRows = 50_000)
    {
        _filter = filter;
        _maxRows = maxRows;
    }

    public void SetNameResolver(NameResolver names) => _names = names;

    public void AddPacket(MabiPacket p)
    {
        if (!_filter.IsAllowed(p)) return;
        Rows.Add(new PacketRowVm(p, _names));
        while (Rows.Count > _maxRows) Rows.RemoveAt(0);
    }

    public void Clear() => Rows.Clear();
}
