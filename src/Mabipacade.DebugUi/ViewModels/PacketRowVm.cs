using Mabipacade.Core.Model;

namespace Mabipacade.DebugUi.ViewModels;

public sealed class PacketRowVm
{
    public MabiPacket Packet { get; }
    public string Time { get; }
    public string Dir { get; }
    public string Op { get; }
    public string EntityId { get; }
    public string TypeLabel { get; }

    public PacketRowVm(MabiPacket packet)
    {
        Packet = packet;
        Time = packet.TimestampUtc.ToString("HH:mm:ss.fff");
        Dir = packet.Direction == Direction.Inbound ? "in" : "out";
        Op = $"0x{packet.Op:X4}";
        EntityId = packet.EntityId.ToString();
        TypeLabel = packet.Decoded?.GetType().Name ?? "(L2)";
    }
}
