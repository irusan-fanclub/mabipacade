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
}
