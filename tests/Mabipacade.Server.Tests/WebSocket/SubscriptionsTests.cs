using Mabipacade.Core.Diagnostics;
using Mabipacade.Core.Model;
using Mabipacade.Server.WebSocket;

namespace Mabipacade.Server.Tests.WebSocket;

public class SubscriptionsTests
{
    private static MabiPacket Make(ushort op) =>
        new(DateTime.UtcNow, Direction.Inbound, op, 0UL, Array.Empty<MessageElem>(), null);

    [Fact]
    public void Default_AllowsAll()
    {
        var s = new Subscription();
        Assert.True(s.AllowsPacket(Make(0x6984)));
        Assert.True(s.AllowsEvent(new SessionEvent.SessionStart(DateTime.UtcNow, "tw", null)));
    }

    [Fact]
    public void SubscribeToOps_LimitsPackets()
    {
        var s = new Subscription();
        s.ApplyCommand("{\"op\":\"subscribe\",\"kinds\":[\"packet\"],\"ops\":[\"0x6984\",\"0x7926\"]}");
        Assert.True(s.AllowsPacket(Make(0x6984)));
        Assert.True(s.AllowsPacket(Make(0x7926)));
        Assert.False(s.AllowsPacket(Make(0x6985)));
    }

    [Fact]
    public void SubscribeToEventsOnly_DropsPackets()
    {
        var s = new Subscription();
        s.ApplyCommand("{\"op\":\"subscribe\",\"kinds\":[\"event\"]}");
        Assert.False(s.AllowsPacket(Make(0x6984)));
        Assert.True(s.AllowsEvent(new SessionEvent.SessionStart(DateTime.UtcNow, "tw", null)));
    }

    [Fact]
    public void MalformedCommand_LeavesSubscriptionUnchanged()
    {
        var s = new Subscription();
        s.ApplyCommand("not json");
        Assert.True(s.AllowsPacket(Make(0x6984)));  // still default = allow all
    }
}
