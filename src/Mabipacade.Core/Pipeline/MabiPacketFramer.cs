using System.Buffers.Binary;
using Mabipacade.Core.Model;

namespace Mabipacade.Core.Pipeline;

internal enum FrameResult { Ok, NeedMore, FramingError }

internal static class MabiPacketFramer
{
    private const int HeaderSize = 6;
    private const int BodyMinSize = 4 + 8 + 1;
    private const uint MaxPacketLength = 0x100_0000;

    public static FrameResult TryReadOne(ReadOnlySpan<byte> buffer, out MabiPacketSlice? slice, out int consumed)
    {
        slice = null;
        consumed = 0;

        if (buffer.Length < HeaderSize) return FrameResult.NeedMore;

        uint length = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(1, 4));
        byte flag = buffer[5];

        if (length == 0 || length > MaxPacketLength) return FrameResult.FramingError;
        if (flag > 4) return FrameResult.FramingError;

        bool isShort = flag == 1 || flag == 2;
        if (isShort)
        {
            if (buffer.Length < length) return FrameResult.NeedMore;
            if (length < HeaderSize) return FrameResult.FramingError;
            consumed = (int)length;
            return FrameResult.Ok;
        }

        if (length < HeaderSize + BodyMinSize) return FrameResult.FramingError;
        if (buffer.Length < length) return FrameResult.NeedMore;

        var body = buffer.Slice(HeaderSize, (int)length - HeaderSize);
        // Opcode is a full 32-bit BE field. The upper bytes carry a packet
        // "category" (cat 1 / cat 2 on some TW pet/buff packets); truncating to
        // 16 bits collides 0x00021208 with 0x1208. Aura's Op is `int` for the
        // same reason.
        uint op = BinaryPrimitives.ReadUInt32BigEndian(body.Slice(0, 4));
        ulong entityId = BinaryPrimitives.ReadUInt64BigEndian(body.Slice(4, 8));
        var msg = body.Slice(12).ToArray();

        slice = new MabiPacketSlice(op, entityId, msg);
        consumed = (int)length;
        return FrameResult.Ok;
    }
}
