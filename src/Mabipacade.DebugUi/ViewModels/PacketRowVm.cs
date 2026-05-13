using Mabipacade.Core.Model;
using Mabipacade.DebugUi.Resolution;

namespace Mabipacade.DebugUi.ViewModels;

public sealed class PacketRowVm
{
    public MabiPacket Packet { get; }
    public string Time { get; }
    public string Dir { get; }
    public string Op { get; }
    public string EntityId { get; }
    public string TypeLabel { get; }
    public string SkillName { get; }

    public PacketRowVm(MabiPacket packet, NameResolver? names = null)
    {
        Packet = packet;
        Time = packet.TimestampUtc.ToString("HH:mm:ss.fff");
        Dir = packet.Direction == Direction.Inbound ? "in" : "out";
        Op = $"0x{packet.Op:X4}";
        EntityId = packet.EntityId.ToString();
        TypeLabel = packet.Decoded?.GetType().Name ?? "(L2)";

        if (names is not null)
        {
            var ids = DecodedSkillExtractor.ExtractSkillIds(packet.Decoded);
            var nameList = new List<string>();
            foreach (var id in ids)
            {
                var n = names.TryResolveSkill(id);
                if (n is not null) nameList.Add(n);
            }
            SkillName = nameList.Count == 0 ? string.Empty : string.Join(", ", nameList);
        }
        else
        {
            SkillName = string.Empty;
        }
    }
}
