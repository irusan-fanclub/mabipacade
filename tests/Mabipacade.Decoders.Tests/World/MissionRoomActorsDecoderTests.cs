using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.World;
using static Mabipacade.Decoders.Tests.TestSupport.NestedBodyBytes;

namespace Mabipacade.Decoders.Tests.World;

public class MissionRoomActorsDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x000186A6, new MissionRoomActorsDecoder().Op);
    }

    [Fact]
    public void Decodes_RoomAndKeyedActors()
    {
        // Captured shape (tw-164, 2026-08-19 boss-room start): { Long charId,
        // Long charId, String room, Int count } then count × { String key,
        // Int length, Bin nested appear body }, then { Int, Long charId }.
        var me = NestedBody(
            NLong(0x00100000002B4ABAUL), NByte(5),
            NString("蘑菇嫩煎雞"), NString(""), NString(""), NInt(10002));
        var boss = NestedBody(
            NLong(0x0010F00000000ABCUL), NByte(5),
            NString("#g27_VT"), NString(""), NString(""), NInt(5161));
        var input = Input(
            MessageElem.Long(0x00100000002B4ABAUL), MessageElem.Long(0x00100000002B4ABAUL),
            MessageElem.String("mrd_Bossroom_01"), MessageElem.Int(2),
            MessageElem.String("me"), MessageElem.Int((uint)me.Length), MessageElem.Bin(me),
            MessageElem.String("#g27_VT"), MessageElem.Int((uint)boss.Length), MessageElem.Bin(boss),
            MessageElem.Int(1), MessageElem.Long(0x00100000002B4ABAUL));

        var decoded = Assert.IsType<MissionRoomActors>(new MissionRoomActorsDecoder().Decode(input));

        Assert.Equal(0x00100000002B4ABAUL, decoded.CharacterId);
        Assert.Equal("mrd_Bossroom_01", decoded.RoomName);
        Assert.Equal(2, decoded.DeclaredCount);
        Assert.Equal(2, decoded.Actors.Count);
        Assert.Equal("me", decoded.Actors[0].Key);
        Assert.Equal(0x00100000002B4ABAUL, decoded.Actors[0].EntityId);
        Assert.Equal("蘑菇嫩煎雞", decoded.Actors[0].Name);
        Assert.Equal(10002u, decoded.Actors[0].RaceId);
        Assert.Equal("#g27_VT", decoded.Actors[1].Key);
        Assert.Equal(5161u, decoded.Actors[1].RaceId);
    }

    [Fact]
    public void Decodes_EmptyBody_Defaults()
    {
        var decoded = Assert.IsType<MissionRoomActors>(new MissionRoomActorsDecoder().Decode(Input()));
        Assert.Equal(0UL, decoded.CharacterId);
        Assert.Equal("", decoded.RoomName);
        Assert.Equal(0, decoded.DeclaredCount);
        Assert.Empty(decoded.Actors);
    }

    [Fact]
    public void Decodes_MalformedNestedBody_ActorKeptWithZeroId()
    {
        var input = Input(
            MessageElem.Long(1UL), MessageElem.Long(1UL),
            MessageElem.String("mrd_Bossroom_01"), MessageElem.Int(1),
            MessageElem.String("#x"), MessageElem.Int(3), MessageElem.Bin(new byte[] { 1, 2, 3 }));

        var decoded = Assert.IsType<MissionRoomActors>(new MissionRoomActorsDecoder().Decode(input));

        var a = Assert.Single(decoded.Actors);
        Assert.Equal("#x", a.Key);
        Assert.Equal(0UL, a.EntityId);
        Assert.Null(a.Name);
    }

    private static DecoderInput Input(params MessageElem[] elems) =>
        new(DateTime.UtcNow, Direction.Inbound, 0x000186A6, 0UL, elems);
}
