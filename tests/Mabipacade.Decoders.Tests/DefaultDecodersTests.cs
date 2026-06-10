using Mabipacade.Core.Pipeline;
using Mabipacade.Decoders;

namespace Mabipacade.Decoders.Tests;

public class DefaultDecodersTests
{
    [Fact]
    public void RegisterAll_RegistersManyDistinctOps_NoSilentCollisions()
    {
        var reg = new DecoderRegistry();
        DefaultDecoders.RegisterAll(reg);
        // The registry is keyed by op, so a duplicate Register would silently
        // overwrite and shrink the count. Guard a healthy lower bound.
        Assert.True(reg.RegisteredOps.Count >= 80,
            $"expected >= 80 decoders, got {reg.RegisteredOps.Count}");
    }

    [Fact]
    public void RegisterAll_IncludesKeyOps()
    {
        var reg = new DecoderRegistry();
        DefaultDecoders.RegisterAll(reg);
        // Combat damage, entity appear, pet register, notice, prop, url.
        Assert.Contains((uint)0x6984, reg.RegisteredOps);
        Assert.Contains((uint)0x7926, reg.RegisteredOps);
        Assert.Contains((uint)0x520C, reg.RegisteredOps);
        Assert.Contains((uint)0x9024, reg.RegisteredOps);
        Assert.Contains((uint)0x526D, reg.RegisteredOps);
        Assert.Contains((uint)0x52D0, reg.RegisteredOps);
        Assert.Contains((uint)0x65A2, reg.RegisteredOps);
    }
}
