using Mabipacade.Core.Model;
using Mabipacade.DebugUi.ViewModels;

namespace Mabipacade.DebugUi.Tests.ViewModels;

public class FilterViewModelTests
{
    private static MabiPacket Make(uint op = 0x00006984, ulong entityId = 0UL, object? decoded = null)
        => new(DateTime.UtcNow, Direction.Inbound, op, entityId, Array.Empty<MessageElem>(), decoded);

    [Fact]
    public void Empty_AllowsEverything()
    {
        var vm = new FilterViewModel();
        Assert.True(vm.IsAllowed(Make()));
        Assert.True(vm.IsAllowed(Make(op: 0x0000FFFF)));
    }

    [Fact]
    public void OpText_LimitsToList()
    {
        var vm = new FilterViewModel { OpText = "0x00006984,0x00007926" };
        Assert.True(vm.IsAllowed(Make(op: 0x00006984)));
        Assert.True(vm.IsAllowed(Make(op: 0x00007926)));
        Assert.False(vm.IsAllowed(Make(op: 0x00006985)));
    }

    [Fact]
    public void OpText_InvalidHex_TreatedAsNoFilter()
    {
        var vm = new FilterViewModel { OpText = "garbage" };
        Assert.True(vm.IsAllowed(Make()));
    }

    [Fact]
    public void EntityIdText_MatchesDecimalString()
    {
        var vm = new FilterViewModel { EntityIdText = "234" };
        Assert.True(vm.IsAllowed(Make(entityId: 12345UL)));
        Assert.False(vm.IsAllowed(Make(entityId: 5UL)));
    }

    [Fact]
    public void DecodedOnly_FiltersUndecoded()
    {
        var vm = new FilterViewModel { DecodedOnly = true };
        Assert.False(vm.IsAllowed(Make(decoded: null)));
        Assert.True(vm.IsAllowed(Make(decoded: "x")));
    }
}
