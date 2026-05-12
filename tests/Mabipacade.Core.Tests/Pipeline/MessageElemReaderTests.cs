using Mabipacade.Core.Model;
using Mabipacade.Core.Pipeline;

namespace Mabipacade.Core.Tests.Pipeline;

public class MessageElemReaderTests
{
    // Builds the message prefix: [outer uvarint=0][count uvarint][reserved 0 byte]
    private static byte[] Header(int count) => new byte[] { 0x00, (byte)count, 0x00 };

    [Fact]
    public void Reads_SingleShort_BigEndian()
    {
        // tag=2 (Short), value=59000 (0xE678 BE = 0xE6 0x78)
        var body = Header(1).Concat(new byte[] { 0x02, 0xE6, 0x78 }).ToArray();
        var result = MessageElemReader.TryRead(body, out var elems);
        Assert.Equal(ReadElemsResult.Ok, result);
        Assert.Single(elems);
        Assert.Equal(MessageElemType.Short, elems[0].Type);
        Assert.Equal((ushort)59000, elems[0].AsUInt16());
    }

    [Fact]
    public void Reads_ByteShortIntLong_BigEndian()
    {
        var body = Header(4).Concat(new byte[]
        {
            0x01, 0x2A,                               // Byte(42)
            0x02, 0x00, 0x01,                         // Short(1) BE
            0x03, 0x01, 0x02, 0x03, 0x04,             // Int(0x01020304) BE
            0x04, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08  // Long(0x0102030405060708) BE
        }).ToArray();
        var result = MessageElemReader.TryRead(body, out var elems);
        Assert.Equal(ReadElemsResult.Ok, result);
        Assert.Equal(4, elems.Count);
        Assert.Equal((byte)42, elems[0].AsByte());
        Assert.Equal((ushort)1, elems[1].AsUInt16());
        Assert.Equal(0x01020304u, elems[2].AsUInt32());
        Assert.Equal(0x0102030405060708UL, elems[3].AsUInt64());
    }

    [Fact]
    public void Reads_String_BeLength_StripsTrailingNul()
    {
        // tag=6, length=6 (BE: 0x00 0x06; "hello\0" = 6 bytes; visible 5)
        var body = Header(1).Concat(new byte[]
        {
            0x06, 0x00, 0x06, 0x68, 0x65, 0x6C, 0x6C, 0x6F, 0x00
        }).ToArray();
        var result = MessageElemReader.TryRead(body, out var elems);
        Assert.Equal(ReadElemsResult.Ok, result);
        Assert.Equal("hello", elems[0].AsString());
    }

    [Fact]
    public void Reads_Float_LittleEndian()
    {
        // tag=5, 1.0f = 0x3F800000; in LE wire = 0x00 0x00 0x80 0x3F
        var body = Header(1).Concat(new byte[] { 0x05, 0x00, 0x00, 0x80, 0x3F }).ToArray();
        var result = MessageElemReader.TryRead(body, out var elems);
        Assert.Equal(ReadElemsResult.Ok, result);
        Assert.Equal(1.0f, elems[0].AsFloat());
    }

    [Fact]
    public void Returns_BadBody_OnUnknownTag()
    {
        var body = Header(1).Concat(new byte[] { 0xAA, 0x00 }).ToArray();
        var result = MessageElemReader.TryRead(body, out var elems);
        Assert.Equal(ReadElemsResult.BadBody, result);
        Assert.Empty(elems);
    }

    [Fact]
    public void Returns_BadBody_OnTruncatedShort()
    {
        // tag=2 (Short) needs 2 bytes; only 1 supplied
        var body = Header(1).Concat(new byte[] { 0x02, 0x78 }).ToArray();
        var result = MessageElemReader.TryRead(body, out var elems);
        Assert.Equal(ReadElemsResult.BadBody, result);
    }

    [Fact]
    public void Returns_Ok_OnZeroCount()
    {
        var body = Header(0);
        var result = MessageElemReader.TryRead(body, out var elems);
        Assert.Equal(ReadElemsResult.Ok, result);
        Assert.Empty(elems);
    }
}
