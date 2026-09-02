using System.Buffers.Binary;

namespace Mabipacade.Decoders.Tests.TestSupport;

/// <summary>
/// Builds the nested packet-body byte blobs that batch opcodes (0x5334,
/// 0x186A6) carry inside Bin elements: zeroed op + id, then the standard
/// [uvarint][count][unused][tagged elements] message of the wire format.
/// </summary>
internal static class NestedBodyBytes
{
    public static byte[] NestedBody(params byte[][] elems)
    {
        var body = new List<byte>();
        body.AddRange(new byte[12]);        // nested op and id, zero on the wire
        body.Add(0);                        // discarded uvarint
        body.Add((byte)elems.Length);       // element count uvarint
        body.Add(0);                        // unused byte
        foreach (var e in elems) body.AddRange(e);
        return body.ToArray();
    }

    public static byte[] NByte(byte v) => new byte[] { 1, v };

    public static byte[] NInt(uint v)
    {
        var b = new byte[5];
        b[0] = 3;
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(1), v);
        return b;
    }

    public static byte[] NLong(ulong v)
    {
        var b = new byte[9];
        b[0] = 4;
        BinaryPrimitives.WriteUInt64BigEndian(b.AsSpan(1), v);
        return b;
    }

    public static byte[] NString(string s)
    {
        var utf8 = System.Text.Encoding.UTF8.GetBytes(s);
        var b = new byte[3 + utf8.Length + 1];
        b[0] = 6;
        BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(1), (ushort)(utf8.Length + 1));
        utf8.CopyTo(b, 3);
        return b;
    }
}
