using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Snapshot;

namespace Mabipacade.Decoders.Tests.Snapshot;

public class ChannelCharacterInfoDecoderTests
{
    [Fact]
    public void Op_Matches() => Assert.Equal(0x00005209u, new ChannelCharacterInfoDecoder().Op);

    [Fact]
    public void Decodes_Prefix_Regens_Bag_Skills_Master()
    {
        var e = new MessageElem[260].ToList();
        for (int i = 0; i < 260; i++) e[i] = MessageElem.Int(0); // filler

        // Fixed prefix (TW indices)
        e[1] = MessageElem.Long(4504699143795610);   // EntityId
        e[3] = MessageElem.String("小雞七號");          // Name
        e[6] = MessageElem.Int(490289);               // RaceId
        e[12] = MessageElem.Float(1.7f);              // Height
        e[13] = MessageElem.Float(1.0f);              // Weight
        e[16] = MessageElem.Int(35004);               // Region
        e[17] = MessageElem.Int(42245);               // PosX
        e[18] = MessageElem.Int(40225);               // PosY
        e[19] = MessageElem.Byte(174);                // Direction
        e[22] = MessageElem.Int(0x00FFFFFF);          // Color1
        e[25] = MessageElem.Float(1600.84f);          // CombatPower
        e[32] = MessageElem.Float(1353.8f);           // Life
        e[34] = MessageElem.Float(803.8f);            // LifeMaxBase
        e[35] = MessageElem.Float(550f);              // LifeMaxMod -> LifeMax 1353.8
        e[44] = MessageElem.Short(200);               // Level
        e[47] = MessageElem.Short(0);                 // Rebirth
        e[51] = MessageElem.Float(394.25f);           // Str base

        // Regen list: 1 regen, 7 elems
        e[207] = MessageElem.Int(1);                  // regen count
        e[208] = MessageElem.Int(2);                  // id
        e[209] = MessageElem.Float(0.05f);            // change
        e[210] = MessageElem.Int(unchecked((uint)-26)); // timeLeft (signed on wire)
        e[211] = MessageElem.Int(32);                 // stat
        e[212] = MessageElem.Byte(0);
        e[213] = MessageElem.Float(820.5f);           // max
        e[214] = MessageElem.Byte(1);

        // 25 blank elems already filler (Int 0) at 215..239 — fine.

        // Bag header at 240: 5x7, 1 item
        e[240] = MessageElem.Int(5);
        e[241] = MessageElem.Int(7);
        e[242] = MessageElem.Int(1);
        // ItemEntry (11 elems, attN=0) at 243
        e[243] = MessageElem.Long(22518895129054312);          // instanceId
        e[244] = MessageElem.Byte(2);                          // marker
        e[245] = MessageElem.Bin(BuildItemCore(2, 51034, 100, 4, 6)); // core
        e[246] = MessageElem.Bin(new byte[100]);              // custom
        e[247] = MessageElem.String("");                      // sig
        e[248] = MessageElem.String("");                      // memo
        e[249] = MessageElem.Byte(0);                         // attN
        e[250] = MessageElem.Long(0);                         // questId
        e[251] = MessageElem.Byte(0);                         // flagA
        e[252] = MessageElem.Byte(0);                         // flagB
        e[253] = MessageElem.Long(4504699143795610);          // owner

        // Skill book at 254: keywords 0, skills 1, 1 bin
        e[254] = MessageElem.Short(0);                        // keyword count
        e[255] = MessageElem.Short(1);                        // skill count
        e[256] = MessageElem.Bin(BuildSkill(20001, 10));      // Smash R1
        // Tail: master id (twice) + master name + metadata
        e[257] = MessageElem.Long(4503599628180874);
        e[258] = MessageElem.String("嵐嵐小雞");
        e[259] = MessageElem.Long(4503599628180874);

        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x00005209, 0UL, e);
        var r = (ChannelCharacterInfo)new ChannelCharacterInfoDecoder().Decode(input);

        Assert.Equal(4504699143795610UL, r.EntityId);
        Assert.Equal("小雞七號", r.Name);
        Assert.Equal(490289u, r.RaceId);
        Assert.Equal(35004u, r.RegionId);
        Assert.Equal(1600.84f, r.CombatPower, 2);
        Assert.Equal(1353.8f, r.LifeMax, 1);
        Assert.Equal((ushort)200, r.Level);

        Assert.Single(r.Regens);
        Assert.Equal(2u, r.Regens[0].Id);
        Assert.Equal(32u, r.Regens[0].Stat);

        Assert.Equal(5u, r.BagWidth);
        Assert.Equal(7u, r.BagHeight);
        Assert.Single(r.Items);
        Assert.Equal(51034u, r.Items[0].ItemId);
        Assert.Equal(100u, r.Items[0].Quantity);
        Assert.Equal(4u, r.Items[0].PosX);
        Assert.Equal(6u, r.Items[0].PosY);

        Assert.Single(r.Skills);
        Assert.Equal((ushort)20001, r.Skills[0].SkillId);
        Assert.Equal((byte)10, r.Skills[0].Rank);

        Assert.Equal(4503599628180874UL, r.MasterId);
        Assert.Equal("嵐嵐小雞", r.MasterName);
    }

    [Fact]
    public void Decodes_MalformedInput_NoThrow()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x00005209, 0UL,
            new List<MessageElem> { MessageElem.Byte(1), MessageElem.Long(42) });
        var r = (ChannelCharacterInfo)new ChannelCharacterInfoDecoder().Decode(input);
        Assert.Equal(42UL, r.EntityId);   // idx 1 holds Long(42)
        Assert.Empty(r.Items);
        Assert.Empty(r.Skills);
    }

    private static byte[] BuildItemCore(uint recType, uint itemId, uint qty, uint x, uint y)
    {
        var b = new byte[80];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(0), recType);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4), itemId);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(36), qty);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(44), x);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(48), y);
        return b;
    }

    private static byte[] BuildSkill(ushort skillId, byte rank)
    {
        var b = new byte[38];
        b[0] = (byte)(skillId & 0xFF);
        b[1] = (byte)(skillId >> 8);
        b[4] = rank;
        return b;
    }
}
