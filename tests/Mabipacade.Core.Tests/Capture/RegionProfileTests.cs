using System.Net;
using Mabipacade.Core.Capture;

namespace Mabipacade.Core.Tests.Capture;

public class RegionProfileTests
{
    [Fact]
    public void Contains_ReturnsTrue_WhenIpInRange()
    {
        var profile = new RegionProfile(
            "test",
            new[] { new IpRange(IPAddress.Parse("10.0.0.0"), IPAddress.Parse("10.0.0.255")) },
            new ushort[] { 11000 });
        Assert.True(profile.Contains(IPAddress.Parse("10.0.0.42"), 11000));
        Assert.False(profile.Contains(IPAddress.Parse("11.0.0.1"), 11000));
        Assert.False(profile.Contains(IPAddress.Parse("10.0.0.42"), 22222));
    }

    [Fact]
    public void Taiwan_IsAvailable()
    {
        Assert.NotNull(RegionProfiles.Taiwan);
        Assert.Equal("tw", RegionProfiles.Taiwan.Name);
    }

    [Fact]
    public void Contains_PortOnlyProfile_MatchesAnyIp_OnKnownPort()
    {
        var profile = new RegionProfile("port-only",
            Array.Empty<IpRange>(),
            new ushort[] { 11000 });
        Assert.True(profile.Contains(IPAddress.Parse("1.2.3.4"), 11000));
        Assert.True(profile.Contains(IPAddress.Parse("203.0.113.42"), 11000));
        Assert.False(profile.Contains(IPAddress.Parse("1.2.3.4"), 22222));
    }

    [Fact]
    public void Contains_Taiwan_MatchesPort11000_AnyIp()
    {
        Assert.True(RegionProfiles.Taiwan.Contains(IPAddress.Parse("61.218.1.2"), 11000));
        Assert.False(RegionProfiles.Taiwan.Contains(IPAddress.Parse("61.218.1.2"), 22222));
    }

    [Fact]
    public void Contains_Taiwan_MatchesChannelPortRange_11000To11999()
    {
        // Live TW channel servers sit above the 11000 login port (observed 11022).
        Assert.True(RegionProfiles.Taiwan.Contains(IPAddress.Parse("210.208.80.34"), 11022));
        Assert.True(RegionProfiles.Taiwan.Contains(IPAddress.Parse("210.208.80.34"), 11999));
        Assert.False(RegionProfiles.Taiwan.Contains(IPAddress.Parse("210.208.80.10"), 8004));
        Assert.False(RegionProfiles.Taiwan.Contains(IPAddress.Parse("210.208.80.34"), 12000));
    }

    [Fact]
    public void Contains_PortRangeProfile_MatchesPortsInsideRange()
    {
        var profile = new RegionProfile("range-only",
            Array.Empty<IpRange>(),
            Array.Empty<ushort>(),
            new[] { new PortRange(11000, 11999) });
        Assert.True(profile.Contains(IPAddress.Parse("1.2.3.4"), 11000));
        Assert.True(profile.Contains(IPAddress.Parse("1.2.3.4"), 11500));
        Assert.True(profile.Contains(IPAddress.Parse("1.2.3.4"), 11999));
        Assert.False(profile.Contains(IPAddress.Parse("1.2.3.4"), 10999));
        Assert.False(profile.Contains(IPAddress.Parse("1.2.3.4"), 12000));
    }

    [Fact]
    public void Contains_Returns_False_ForIpv6()
    {
        var profile = new RegionProfile("test",
            new[] { new IpRange(IPAddress.Parse("10.0.0.0"), IPAddress.Parse("10.0.0.255")) },
            new ushort[] { 11000 });
        Assert.False(profile.Contains(IPAddress.Parse("::1"), 11000));
        Assert.False(profile.Contains(IPAddress.Parse("fe80::1"), 11000));
    }
}
