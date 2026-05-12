using System.Buffers.Binary;

namespace Mabipacade.Core.Tests.Pipeline;

internal static class TestPacketBuilder
{
    // Layout: [sign:1][length:4 LE][flag:1][op:4 BE][entityId:8 BE][bodyTail...]
    public static byte[] BuildNormal(ushort op, ulong entityId, byte[] bodyTail)
    {
        int total = 6 + 4 + 8 + bodyTail.Length;
        var buf = new byte[total];
        buf[0] = 0x00;                                                        // sign
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(1, 4), (uint)total);
        buf[5] = 0x00;                                                        // flag = normal
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(6, 4), op);
        BinaryPrimitives.WriteUInt64BigEndian(buf.AsSpan(10, 8), entityId);
        bodyTail.CopyTo(buf.AsSpan(18));
        return buf;
    }

    public static byte[] BuildShort(byte flag, int payloadLength)
    {
        int total = 6 + payloadLength;
        var buf = new byte[total];
        buf[0] = 0x00;
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(1, 4), (uint)total);
        buf[5] = flag;
        return buf;
    }
}
