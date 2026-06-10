using Mabipacade.Core.Model;

namespace Mabipacade.Core.Tests.Model;

public class MabiPacketTests
{
    [Fact]
    public void Construct_PreservesAllFields()
    {
        var ts = new DateTime(2026, 5, 13, 8, 0, 0, DateTimeKind.Utc);
        var elems = new[] { MessageElem.Short(59000) };
        var packet = new MabiPacket(ts, Direction.Inbound, 0x00006984, 12345UL, elems, Decoded: null);

        Assert.Equal(ts, packet.TimestampUtc);
        Assert.Equal(Direction.Inbound, packet.Direction);
        Assert.Equal((uint)0x00006984, packet.Op);
        Assert.Equal(12345UL, packet.EntityId);
        Assert.Single(packet.Elems);
        Assert.Null(packet.Decoded);
    }

    [Fact]
    public void Records_AreEqual_ByValue()
    {
        var ts = DateTime.UtcNow;
        var elems = new[] { MessageElem.Short(1) };
        var a = new MabiPacket(ts, Direction.Inbound, 0x00000001, 0UL, elems, null);
        var b = new MabiPacket(ts, Direction.Inbound, 0x00000001, 0UL, elems, null);
        Assert.Equal(a, b);
    }
}
