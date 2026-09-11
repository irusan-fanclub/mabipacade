using System.Buffers.Binary;
using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Combat;

namespace Mabipacade.Decoders.Tests.Combat;

public class CombatActionPackDecoderTests
{
    private const ulong Broadcast = 0x3000000000000000UL;
    private const ulong AttackerEid = 0x0010F0000023_9D07UL;
    private const ulong VictimEid = 0x0010F0000023_9D99UL;

    [Fact]
    public void Op_Is7926()
    {
        Assert.Equal((uint)0x00007926, new CombatActionPackDecoder().Op);
    }

    [Fact]
    public void AttackerId_ComesFromSubRecord_NotThePacketEntityId()
    {
        // The packet's own entity id is the broadcast constant and identifies nobody;
        // reporting it as the attacker was the previous behaviour and was always wrong.
        var pack = Decode(Broadcast, Attacker(skillId: 27203), Victim(damage: 20836.37f));

        Assert.Equal(AttackerEid, pack.AttackerId);
        Assert.NotEqual(Broadcast, pack.AttackerId);
    }

    [Fact]
    public void Decodes_BothRecordKinds()
    {
        var pack = Decode(Broadcast, Attacker(skillId: 27203), Victim(damage: 20836.37f));

        Assert.Equal(2, pack.Sub.Count);
        Assert.True(pack.Sub[0].IsAttacker);
        Assert.False(pack.Sub[1].IsAttacker);
        Assert.Equal(3311785u, pack.PackId);
    }

    [Fact]
    public void VictimReactionSkill_DoesNotLandInSkillId()
    {
        // A victim answering with Defense must not be reported as having used Defense as an
        // attack; the attack skill is the attacker record's.
        var pack = Decode(Broadcast, Attacker(skillId: 27203), Victim(damage: 100f, reactionSkill: 20001));

        var attacker = pack.Sub[0];
        var victim = pack.Sub[1];
        Assert.Equal(27203, attacker.SkillId);
        Assert.Equal(0, attacker.ReactionSkill);
        Assert.Equal(0, victim.SkillId);
        Assert.Equal(20001, victim.ReactionSkill);
    }

    [Fact]
    public void AttackerTarget_IsEntityId_WhenPointFlagClear()
    {
        var pack = Decode(Broadcast, Attacker(skillId: 23002, flags: 0x2, target: VictimEid));

        var info = pack.Sub[0].Attacker!;
        Assert.Null(info.TargetPoint);
        Assert.Equal(VictimEid, info.TargetEntityId);
    }

    [Fact]
    public void AttackerTarget_IsPoint_WhenPointFlagSet()
    {
        // Worked example from the capture: region 35003, X 105480, Y 107660.
        const ulong packed = 0x300088BB149A1507UL;
        var pack = Decode(Broadcast,
            Attacker(skillId: 27203, flags: 0x40A | 0x08, target: packed, targetEntity: VictimEid));

        var info = pack.Sub[0].Attacker!;
        Assert.NotNull(info.TargetPoint);
        Assert.Equal(35003, info.TargetPoint!.Region);
        Assert.Equal(105480u, info.TargetPoint.X);
        Assert.Equal(107660u, info.TargetPoint.Y);
        Assert.Equal(packed, info.TargetPoint.Raw);

        // The entity actually struck moves to the trailing element in this form.
        Assert.Equal(VictimEid, info.TargetEntityId);
    }

    [Fact]
    public void VictimShortForm_HasNoOptionalBlock_AndStillFindsAttacker()
    {
        var pack = Decode(Broadcast, Victim(damage: 1234.5f, flags: 0x21));

        var v = pack.Sub[0].Victim!;
        Assert.Equal(1234.5f, v.Damage);
        Assert.Null(v.PosX);
        Assert.Null(v.DelayMs);
        Assert.Equal(AttackerEid, v.AttackerId);
    }

    [Fact]
    public void VictimLongForm_ReadsOptionalBlock_AndAttackerFromTail()
    {
        // The optional block is inserted mid-record, so the tail shifts. Reading the attacker
        // by a fixed front index would pick up the delay field instead.
        var pack = Decode(Broadcast, Victim(damage: 45768.82f, flags: 0x1000, longForm: true));

        var v = pack.Sub[0].Victim!;
        Assert.Equal(45768.82f, v.Damage);
        Assert.Equal(105560f, v.PosX);
        Assert.Equal(107400f, v.PosY);
        Assert.Equal(120u, v.DelayMs);
        Assert.Equal(AttackerEid, v.AttackerId);
    }

    [Fact]
    public void NonSubRecordBin_IsIgnored()
    {
        var pack = Decode(Broadcast, Attacker(skillId: 23002), new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x00 });

        Assert.Single(pack.Sub);
        Assert.True(pack.Sub[0].IsAttacker);
    }

    [Fact]
    public void EmptyPacket_DecodesToEmptyPack()
    {
        var decoder = new CombatActionPackDecoder();
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x00007926, Broadcast,
            Array.Empty<MessageElem>());

        var pack = (CombatActionPack)decoder.Decode(input);

        Assert.Empty(pack.Sub);
        Assert.Equal(0u, pack.PackId);
        Assert.Equal(0UL, pack.AttackerId);
    }

    // ---- helpers ------------------------------------------------------------------

    private static CombatActionPack Decode(ulong packetEntityId, params byte[][] subRecords)
    {
        var elems = new List<MessageElem>
        {
            MessageElem.Int(3311785),  // pack id
            MessageElem.Int(0),
            MessageElem.Byte(1),
            MessageElem.Byte(0),
            MessageElem.Byte(1),
            MessageElem.Byte(0),
            MessageElem.Int((uint)subRecords.Length),
        };
        foreach (var r in subRecords)
        {
            elems.Add(MessageElem.Int((uint)r.Length));
            elems.Add(MessageElem.Bin(r));
        }

        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x00007926, packetEntityId, elems);
        return (CombatActionPack)new CombatActionPackDecoder().Decode(input);
    }

    private static byte[] Attacker(
        ushort skillId,
        uint flags = 0x2,
        ulong target = VictimEid,
        ulong? targetEntity = null)
    {
        var b = new BodyBuilder();
        b.Int(3311785).Long(AttackerEid).Byte(2).Short(600).Short(skillId).Short(0).Short(9);
        b.Long(target).Int(flags).Byte(0).Byte(1).Int(0).Int(105560).Int(107400);
        if (targetEntity is ulong te) b.Long(te);
        return b.Build(AttackerEid);
    }

    private static byte[] Victim(
        float damage,
        ushort reactionSkill = 23002,
        uint flags = 0x21,
        bool longForm = false)
    {
        var b = new BodyBuilder();
        b.Int(3311785).Long(VictimEid).Byte(1).Short(2000).Short(reactionSkill).Short(0).Short(10);
        b.Int(flags).Float(damage).Float(0f).Int(0).Int(0).Int(0).Float(-561f).Float(551f);
        if (longForm) b.Float(105560f).Float(107400f).Int(120);
        // Six-element tail; the attacker id is the fourth element from the end.
        b.Byte(32).Int(0).Long(AttackerEid).Int(0).Long(AttackerEid).Byte(0);
        return b.Build(VictimEid);
    }

    /// <summary>
    /// Builds the bytes of a nested 0x7924 record: opcode and entity id big-endian, then a
    /// standard message body (reserved uvarint, element count, reserved zero byte, elements).
    /// </summary>
    private sealed class BodyBuilder
    {
        private readonly List<byte> _elems = new();
        private int _count;

        public BodyBuilder Byte(byte v) { _elems.Add(1); _elems.Add(v); _count++; return this; }

        public BodyBuilder Short(ushort v)
        {
            _elems.Add(2);
            var buf = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(buf, v);
            _elems.AddRange(buf);
            _count++;
            return this;
        }

        public BodyBuilder Int(uint v)
        {
            _elems.Add(3);
            var buf = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(buf, v);
            _elems.AddRange(buf);
            _count++;
            return this;
        }

        public BodyBuilder Long(ulong v)
        {
            _elems.Add(4);
            var buf = new byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(buf, v);
            _elems.AddRange(buf);
            _count++;
            return this;
        }

        public BodyBuilder Float(float v)
        {
            _elems.Add(5);
            var buf = new byte[4];
            BinaryPrimitives.WriteSingleLittleEndian(buf, v);
            _elems.AddRange(buf);
            _count++;
            return this;
        }

        public byte[] Build(ulong entityId)
        {
            var body = new List<byte>();
            body.Add((byte)_elems.Count);   // reserved uvarint: bytes after the count field
            body.Add((byte)_count);         // element count uvarint
            body.Add(0);                    // reserved zero byte
            body.AddRange(_elems);

            var packet = new List<byte>();
            var op = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(op, 0x00007924);
            packet.AddRange(op);
            var eid = new byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(eid, entityId);
            packet.AddRange(eid);
            packet.AddRange(body);
            return packet.ToArray();
        }
    }
}
