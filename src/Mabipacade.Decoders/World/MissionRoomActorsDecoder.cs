using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Entity;

namespace Mabipacade.Decoders.World;

/// <summary>
/// One named actor of a mission-room roster. <see cref="Key"/> is the roster
/// key ("me" for the capturer's own character, otherwise the entity's system
/// name, e.g. "#g27_VT"); the rest comes from the nested appear body.
/// </summary>
public sealed record MissionRoomActor(string Key, ulong EntityId, string? Name, uint? RaceId);

public sealed record MissionRoomActors(ulong CharacterId, string RoomName, int DeclaredCount,
    IReadOnlyList<MissionRoomActor> Actors);

/// <summary>
/// 0x186A6 — mission boss-room roster, seen at encounter start (followed by
/// 0x186A7/0x186A8 acks, boss BGM and the boss 0x520C spawn). Body is
/// { Long charId, Long charId, String roomName, Int count } then count ×
/// { String key, Int length, Bin nested appear body } and a trailing
/// { Int, Long charId }. Nested bodies share 0x5334's convention (zeroed
/// op/id + standard message, 0x520C character layout).
/// </summary>
public sealed class MissionRoomActorsDecoder : IPacketDecoder
{
    public uint Op => 0x000186A6;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        ulong charId = e.Count > 0 && e[0].Type == MessageElemType.Long ? e[0].AsUInt64() : 0UL;
        string room = e.Count > 2 && e[2].Type == MessageElemType.String ? e[2].AsString() : "";
        int declared = e.Count > 3 && e[3].Type == MessageElemType.Int ? (int)e[3].AsUInt32() : 0;

        var actors = new List<MissionRoomActor>();
        for (int i = 4; i + 2 < e.Count; i += 3)
        {
            if (e[i].Type != MessageElemType.String || e[i + 2].Type != MessageElemType.Bin)
                break;
            NestedAppearBody.TryRead(e[i + 2].AsBytes(), out ulong id, out string? name, out uint? raceId);
            actors.Add(new MissionRoomActor(e[i].AsString(), id, name, raceId));
        }

        return new MissionRoomActors(charId, room, declared, actors);
    }
}
