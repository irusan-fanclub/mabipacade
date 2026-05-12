using Mabipacade.Core.Model;

namespace Mabipacade.Core.Tests.Model;

public class MessageElemTests
{
    [Fact]
    public void Byte_ReturnsTagAndValue()
    {
        var e = MessageElem.Byte(42);
        Assert.Equal(MessageElemType.Byte, e.Type);
        Assert.Equal((byte)42, e.AsByte());
    }

    [Fact]
    public void Short_ReturnsTagAndValue()
    {
        var e = MessageElem.Short(59000);
        Assert.Equal(MessageElemType.Short, e.Type);
        Assert.Equal((ushort)59000, e.AsUInt16());
    }

    [Fact]
    public void String_ReturnsTagAndValue()
    {
        var e = MessageElem.String("hello");
        Assert.Equal(MessageElemType.String, e.Type);
        Assert.Equal("hello", e.AsString());
    }

    [Fact]
    public void AsString_Throws_WhenTypeIsByte()
    {
        var e = MessageElem.Byte(1);
        Assert.Throws<InvalidOperationException>(() => e.AsString());
    }
}
