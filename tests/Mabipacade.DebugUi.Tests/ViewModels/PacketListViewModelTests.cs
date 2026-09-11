using Mabipacade.Core.Model;
using Mabipacade.DebugUi.ViewModels;

namespace Mabipacade.DebugUi.Tests.ViewModels;

public class PacketListViewModelTests
{
    private static MabiPacket Make(uint op = 0x6984) =>
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
        vm.AddPacket(Make(op: 0x00000001));
        vm.AddPacket(Make(op: 0x00000002));
        vm.AddPacket(Make(op: 0x00000003));
        vm.AddPacket(Make(op: 0x00000004));
        Assert.Equal(3, vm.Rows.Count);
        Assert.Equal("0x00000002", vm.Rows[0].Op);
        Assert.Equal("0x00000004", vm.Rows[2].Op);
    }

    [Fact]
    public void Add_FilteredOut_DoesNotAppearInRows()
    {
        var filter = new FilterViewModel { OpText = "0x00006984" };
        var vm = new PacketListViewModel(filter, maxRows: 100);
        vm.AddPacket(Make(op: 0x00006984));
        vm.AddPacket(Make(op: 0x00009999));
        Assert.Single(vm.Rows);
        Assert.Equal("0x00006984", vm.Rows[0].Op);
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
