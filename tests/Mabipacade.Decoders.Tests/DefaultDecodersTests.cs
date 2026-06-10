using Mabipacade.Core.Pipeline;
using Mabipacade.Decoders;

namespace Mabipacade.Decoders.Tests;

public class DefaultDecodersTests
{
    [Fact]
    public void RegisterAll_Adds24Decoders()
    {
        var reg = new DecoderRegistry();
        DefaultDecoders.RegisterAll(reg);
        Assert.Equal(24, reg.RegisteredOps.Count);
    }

    [Fact]
    public void RegisterAll_IncludesKeyOps()
    {
        var reg = new DecoderRegistry();
        DefaultDecoders.RegisterAll(reg);
        Assert.Contains((uint)0x6984, reg.RegisteredOps);
        Assert.Contains((uint)0x7926, reg.RegisteredOps);
        Assert.Contains((uint)0x520C, reg.RegisteredOps);
    }
}
