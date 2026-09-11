using Mabipacade.Core.Model;
using Mabipacade.DebugUi.ViewModels;

namespace Mabipacade.DebugUi.Tests.ViewModels;

public class PacketRowVmTests
{
    [Fact]
    public void SkillName_ResolvesFromNameResolver()
    {
        var resolver = new Mabipacade.DebugUi.Resolution.NameResolver(
            new Dictionary<int, Mabipacade.DebugUi.Resolution.SkillNameEntry>
            {
                [59000] = new(59000, "Final Hit", "終結一擊"),
            });
        var packet = new MabiPacket(DateTime.UtcNow, Direction.Inbound, 0x00006984, 0UL,
            Array.Empty<MessageElem>(),
            Decoded: new Mabipacade.Decoders.Skills.PlayerSkillPrepareStart(59000));
        var row = new PacketRowVm(packet, resolver);
        Assert.Equal("終結一擊", row.SkillName);
    }


    [Fact]
    public void Construct_FormatsDisplayFields()
    {
        var ts = new DateTime(2026, 5, 13, 8, 23, 11, 842, DateTimeKind.Utc);
        var packet = new MabiPacket(ts, Direction.Inbound, 0x00006984, 12345UL,
            new[] { MessageElem.Short(1) }, Decoded: null);
        var row = new PacketRowVm(packet);

        // Local time, so the grid matches the clock on the wall. Derived rather
        // than hard-coded: a literal would bake in the author's timezone.
        Assert.Equal(ts.ToLocalTime().ToString("HH:mm:ss.fff"), row.Time);
        Assert.Equal("in", row.Dir);
        Assert.Equal("0x00006984", row.Op);
        Assert.Equal("12345", row.EntityId);
        Assert.Equal("(L2)", row.TypeLabel);
        Assert.Same(packet, row.Packet);
    }

    [Fact]
    public void TypeLabel_ShowsDecodedTypeName()
    {
        var packet = new MabiPacket(DateTime.UtcNow, Direction.Inbound, 0x00006984, 0UL,
            Array.Empty<MessageElem>(), Decoded: "anything");
        var row = new PacketRowVm(packet);
        Assert.Equal("String", row.TypeLabel);
    }
}
