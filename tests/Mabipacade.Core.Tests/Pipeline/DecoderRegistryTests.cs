using Mabipacade.Core.Model;
using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Core.Tests.Pipeline;

public class DecoderRegistryTests
{
    private sealed class FakeDecoder : IPacketDecoder
    {
        public uint Op { get; }
        public FakeDecoder(uint op) { Op = op; }
        public object Decode(DecoderInput input) => "decoded";
    }

    [Fact]
    public void Register_AndLookup()
    {
        var reg = new DecoderRegistry();
        reg.Register(new FakeDecoder(0x6984));
        Assert.True(reg.TryGet(0x6984, out var d));
        Assert.Equal((uint)0x6984, d!.Op);
    }

    [Fact]
    public void TryGet_ReturnsFalse_WhenMissing()
    {
        var reg = new DecoderRegistry();
        Assert.False(reg.TryGet(0x6984, out _));
    }

    [Fact]
    public void Register_ReplacesPrevious()
    {
        var reg = new DecoderRegistry();
        reg.Register(new FakeDecoder(0x6984));
        var second = new FakeDecoder(0x6984);
        reg.Register(second);
        reg.TryGet(0x6984, out var d);
        Assert.Same(second, d);
    }
}
