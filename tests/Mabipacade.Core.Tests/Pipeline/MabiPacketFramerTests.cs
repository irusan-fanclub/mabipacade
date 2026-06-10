using Mabipacade.Core.Model;
using Mabipacade.Core.Pipeline;

namespace Mabipacade.Core.Tests.Pipeline;

public class MabiPacketFramerTests
{
    [Fact]
    public void Ok_ParsesSingleMinimalPacket()
    {
        var bytes = TestPacketBuilder.BuildNormal(op: 0x6984, entityId: 0x12345678AABBCCDDUL,
                                                  bodyTail: new byte[] { 0x00 });
        var result = MabiPacketFramer.TryReadOne(bytes, out var slice, out int consumed);
        Assert.Equal(FrameResult.Ok, result);
        Assert.NotNull(slice);
        Assert.Equal((uint)0x6984, slice!.Op);
        Assert.Equal(0x12345678AABBCCDDUL, slice.EntityId);
        Assert.Equal(bytes.Length, consumed);
    }

    [Fact]
    public void Ok_PreservesFull32BitOpcode_NoTruncation()
    {
        // TW opcodes carry a category byte in the upper 16 bits (e.g. 0x00021208
        // buff-state, 0x0001FBD4 pet). Narrowing to ushort would collide these
        // with 0x1208 / 0xFBD4. The framer must keep all 32 bits.
        var bytes = TestPacketBuilder.BuildNormal(op: 0x00021208, entityId: 0UL,
                                                  bodyTail: new byte[] { 0x00 });
        var result = MabiPacketFramer.TryReadOne(bytes, out var slice, out _);
        Assert.Equal(FrameResult.Ok, result);
        Assert.Equal(0x00021208u, slice!.Op);
        Assert.NotEqual(0x1208u, slice.Op);
    }

    [Fact]
    public void Ok_ShortPacket_FlagOne_ReturnsNullSlice()
    {
        var bytes = TestPacketBuilder.BuildShort(flag: 1, payloadLength: 8);
        var result = MabiPacketFramer.TryReadOne(bytes, out var slice, out int consumed);
        Assert.Equal(FrameResult.Ok, result);
        Assert.Null(slice);
        Assert.Equal(bytes.Length, consumed);
    }

    [Fact]
    public void NeedMore_WhenLengthExceedsBuffer()
    {
        var bytes = TestPacketBuilder.BuildNormal(op: 0x6984, entityId: 0UL,
                                                  bodyTail: new byte[80]);
        var truncated = bytes[..(bytes.Length / 2)];
        var result = MabiPacketFramer.TryReadOne(truncated, out _, out int consumed);
        Assert.Equal(FrameResult.NeedMore, result);
        Assert.Equal(0, consumed);
    }

    [Fact]
    public void FramingError_OnObsceneLength()
    {
        // sign=0, length=0xFFFFFFFF, flag=0
        var bytes = new byte[] { 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00 };
        var result = MabiPacketFramer.TryReadOne(bytes, out _, out _);
        Assert.Equal(FrameResult.FramingError, result);
    }

    [Fact]
    public void FramingError_OnInvalidFlag()
    {
        // sign=0, length=0x13 (=19, valid), flag=5 (only 0..4 legal)
        var bytes = new byte[19];
        bytes[0] = 0x00;
        bytes[1] = 0x13;
        bytes[2] = 0x00; bytes[3] = 0x00; bytes[4] = 0x00;
        bytes[5] = 0x05;
        var result = MabiPacketFramer.TryReadOne(bytes, out _, out _);
        Assert.Equal(FrameResult.FramingError, result);
    }

    [Fact]
    public void Ok_ConsumesExactly_LeavesTrailing()
    {
        var p1 = TestPacketBuilder.BuildNormal(op: 0x6984, entityId: 0UL, bodyTail: new byte[] { 0x00 });
        var p2 = TestPacketBuilder.BuildNormal(op: 0x6985, entityId: 1UL, bodyTail: new byte[] { 0x00, 0x00, 0x00 });
        var combined = p1.Concat(p2).ToArray();

        Assert.Equal(FrameResult.Ok, MabiPacketFramer.TryReadOne(combined, out var first, out int c1));
        Assert.Equal((uint)0x6984, first!.Op);
        Assert.Equal(p1.Length, c1);

        Assert.Equal(FrameResult.Ok, MabiPacketFramer.TryReadOne(combined.AsSpan(c1), out var second, out int c2));
        Assert.Equal((uint)0x6985, second!.Op);
        Assert.Equal(p2.Length, c2);
    }
}
