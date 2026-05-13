using Mabipacade.Core.Model;
using Mabipacade.DebugUi.ViewModels;

namespace Mabipacade.DebugUi.Tests.ViewModels;

public class PacketDetailViewModelTests
{
    [Fact]
    public void SelectedRow_Null_HasEmptyDecoded()
    {
        var vm = new PacketDetailViewModel { SelectedRow = null };
        Assert.Equal("no packet selected", vm.DecodedJson);
        Assert.Empty(vm.HexLines);
        Assert.Empty(vm.ElemNodes);
    }

    [Fact]
    public void SelectedRow_WithDecoded_RendersJson()
    {
        var packet = new MabiPacket(DateTime.UtcNow, Direction.Inbound, 0x6984, 1UL,
            new[] { MessageElem.Short(42) },
            Decoded: new { skillId = 59000 });
        var vm = new PacketDetailViewModel { SelectedRow = new PacketRowVm(packet) };
        Assert.Contains("skillId", vm.DecodedJson);
        Assert.Contains("59000", vm.DecodedJson);
        Assert.Single(vm.ElemNodes);
    }

    [Fact]
    public void SelectedRow_NoDecoded_ReportsNoDecoder()
    {
        var packet = new MabiPacket(DateTime.UtcNow, Direction.Inbound, 0xFFFF, 0UL,
            Array.Empty<MessageElem>(), null);
        var vm = new PacketDetailViewModel { SelectedRow = new PacketRowVm(packet) };
        Assert.Contains("no decoder registered", vm.DecodedJson);
    }
}
