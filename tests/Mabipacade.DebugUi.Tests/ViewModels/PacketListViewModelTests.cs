using Mabipacade.Core.Model;
using Mabipacade.DebugUi.ViewModels;

namespace Mabipacade.DebugUi.Tests.ViewModels;

public class PacketListViewModelTests
{
    private static MabiPacket Make(ushort op = 0x6984) =>
        new(DateTime.UtcNow, Direction.Inbound, op, 0UL, Array.Empty<MessageElem>(), null);

    [Fact]
    public void Add_AppendsToRows()
    {
        var filter = new FilterViewModel();
        var vm = new PacketListViewModel(filter, maxRows: 100);
        vm.AddPacket(Make());
        vm.AddPacket(Make());
        Assert.Equal(2, vm.Rows.Count);
    }

    [Fact]
    public void Add_BeyondCapacity_EvictsOldest()
    {
        var filter = new FilterViewModel();
        var vm = new PacketListViewModel(filter, maxRows: 3);
        vm.AddPacket(Make(op: 0x0001));
        vm.AddPacket(Make(op: 0x0002));
        vm.AddPacket(Make(op: 0x0003));
        vm.AddPacket(Make(op: 0x0004));
        Assert.Equal(3, vm.Rows.Count);
        Assert.Equal("0x0002", vm.Rows[0].Op);
        Assert.Equal("0x0004", vm.Rows[2].Op);
    }

    [Fact]
    public void Add_FilteredOut_DoesNotAppearInRows()
    {
        var filter = new FilterViewModel { OpText = "0x6984" };
        var vm = new PacketListViewModel(filter, maxRows: 100);
        vm.AddPacket(Make(op: 0x6984));
        vm.AddPacket(Make(op: 0x9999));
        Assert.Single(vm.Rows);
        Assert.Equal("0x6984", vm.Rows[0].Op);
    }

    [Fact]
    public void Clear_EmptiesRows()
    {
        var vm = new PacketListViewModel(new FilterViewModel(), maxRows: 100);
        vm.AddPacket(Make());
        vm.Clear();
        Assert.Empty(vm.Rows);
    }
}
