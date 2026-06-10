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
        var packet = new MabiPacket(DateTime.UtcNow, Direction.Inbound, 0x00006984, 1UL,
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
        var packet = new MabiPacket(DateTime.UtcNow, Direction.Inbound, 0x0000FFFF, 0UL,
            Array.Empty<MessageElem>(), null);
        var vm = new PacketDetailViewModel { SelectedRow = new PacketRowVm(packet) };
        Assert.Contains("no decoder registered", vm.DecodedJson);
    }

    [Fact]
    public void SelectedRow_WithBody_PopulatesHex()
    {
        var bytes = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10 };
        var packet = new MabiPacket(DateTime.UtcNow, Direction.Inbound, 0x00006984, 0UL,
            Array.Empty<MessageElem>(), Decoded: null) { Body = bytes };
        var vm = new PacketDetailViewModel { SelectedRow = new PacketRowVm(packet) };
        Assert.Equal(2, vm.HexLines.Count);     // 16 bytes per line; 17 bytes → 2 lines
    }
}
