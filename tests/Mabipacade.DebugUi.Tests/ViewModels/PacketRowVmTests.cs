using Mabipacade.Core.Model;
using Mabipacade.DebugUi.ViewModels;

namespace Mabipacade.DebugUi.Tests.ViewModels;

public class PacketRowVmTests
{
    [Fact]
    public void Construct_FormatsDisplayFields()
    {
        var ts = new DateTime(2026, 5, 13, 8, 23, 11, 842, DateTimeKind.Utc);
        var packet = new MabiPacket(ts, Direction.Inbound, 0x6984, 12345UL,
            new[] { MessageElem.Short(1) }, Decoded: null);
        var row = new PacketRowVm(packet);

        Assert.Equal("08:23:11.842", row.Time);
        Assert.Equal("in", row.Dir);
        Assert.Equal("0x6984", row.Op);
        Assert.Equal("12345", row.EntityId);
        Assert.Equal("(L2)", row.TypeLabel);
        Assert.Same(packet, row.Packet);
    }

    [Fact]
    public void TypeLabel_ShowsDecodedTypeName()
    {
        var packet = new MabiPacket(DateTime.UtcNow, Direction.Inbound, 0x6984, 0UL,
            Array.Empty<MessageElem>(), Decoded: "anything");
        var row = new PacketRowVm(packet);
        Assert.Equal("String", row.TypeLabel);
    }
}
