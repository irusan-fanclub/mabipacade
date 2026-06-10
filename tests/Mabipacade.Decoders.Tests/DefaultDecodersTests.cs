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
        Assert.Contains((uint)0x00006984, reg.RegisteredOps);
        Assert.Contains((uint)0x00007926, reg.RegisteredOps);
        Assert.Contains((uint)0x0000520C, reg.RegisteredOps);
        Assert.Contains((uint)0x00009024, reg.RegisteredOps);
        Assert.Contains((uint)0x0000526D, reg.RegisteredOps);
        Assert.Contains((uint)0x000052D0, reg.RegisteredOps);
        Assert.Contains((uint)0x000065A2, reg.RegisteredOps);
    }
}
