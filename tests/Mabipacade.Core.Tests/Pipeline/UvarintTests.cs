using Mabipacade.Core.Pipeline;

namespace Mabipacade.Core.Tests.Pipeline;

public class UvarintTests
{
    [Fact]
    public void Reads_SingleByte_LowValue()
    {
        var data = new byte[] { 0x05 };
        Assert.True(Uvarint.TryRead(data, out ulong value, out int consumed));
        Assert.Equal(5UL, value);
        Assert.Equal(1, consumed);
    }

    [Fact]
    public void Reads_TwoBytes_BoundaryValue()
    {
        // 128 = 0x80 = 0b10000000 → uvarint: 0x80 0x01
        var data = new byte[] { 0x80, 0x01 };
        Assert.True(Uvarint.TryRead(data, out ulong value, out int consumed));
        Assert.Equal(128UL, value);
        Assert.Equal(2, consumed);
    }

    [Fact]
    public void Reads_Zero()
    {
        Assert.True(Uvarint.TryRead(new byte[] { 0x00 }, out ulong value, out int consumed));
        Assert.Equal(0UL, value);
        Assert.Equal(1, consumed);
    }

    [Fact]
    public void Returns_False_OnTruncated()
    {
        // 0x80 alone = high bit set, no continuation byte
        var data = new byte[] { 0x80 };
        Assert.False(Uvarint.TryRead(data, out _, out _));
    }
}
