using System.Net;
using Mabipacade.Core.Model;
using Mabipacade.Core.Pipeline;

namespace Mabipacade.Core.Tests.Pipeline;

public class TcpReassemblerTests
{
    private static readonly IPAddress Server = IPAddress.Parse("10.0.0.1");
    private static readonly IPAddress Client = IPAddress.Parse("10.0.0.2");

    private static TcpFrame Frame(uint seq, byte[] payload)
        => new(Server, 11000, Client, 50000, seq, payload, DateTime.UtcNow);

    [Fact]
    public void Accept_InOrder_ConcatsPayloads()
    {
        var r = new TcpReassembler();
        r.Feed(Frame(1000, new byte[] { 1, 2, 3 }));
        r.Feed(Frame(1003, new byte[] { 4, 5 }));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, r.GetBuffer().ToArray());
    }

    [Fact]
    public void Accept_OutOfOrder_ReordersBeforeEmit()
    {
        var r = new TcpReassembler();
        r.Feed(Frame(1003, new byte[] { 4, 5 }));
        r.Feed(Frame(1000, new byte[] { 1, 2, 3 }));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, r.GetBuffer().ToArray());
    }

    [Fact]
    public void Drops_Duplicate()
    {
        var r = new TcpReassembler();
        r.Feed(Frame(1000, new byte[] { 1, 2, 3 }));
        r.Feed(Frame(1000, new byte[] { 1, 2, 3 }));
        Assert.Equal(new byte[] { 1, 2, 3 }, r.GetBuffer().ToArray());
    }

    [Fact]
    public void Consume_RemovesBytesFromHead()
    {
        var r = new TcpReassembler();
        r.Feed(Frame(1000, new byte[] { 1, 2, 3, 4, 5 }));
        r.Consume(3);
        Assert.Equal(new byte[] { 4, 5 }, r.GetBuffer().ToArray());
    }

    [Fact]
    public void Reset_ClearsBuffer()
    {
        var r = new TcpReassembler();
        r.Feed(Frame(1000, new byte[] { 1, 2, 3 }));
        r.Reset();
        Assert.True(r.GetBuffer().IsEmpty);
    }
}
