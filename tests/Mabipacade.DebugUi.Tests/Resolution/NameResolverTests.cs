using Mabipacade.DebugUi.Resolution;

namespace Mabipacade.DebugUi.Tests.Resolution;

public class NameResolverTests
{
    [Fact]
    public void Empty_ReturnsNull()
    {
        Assert.Null(NameResolver.Empty.TryResolveSkill(271));
        Assert.Equal(0, NameResolver.Empty.SkillCount);
    }

    [Fact]
    public void TryResolveSkill_ReturnsLocalName()
    {
        var resolver = new NameResolver(new Dictionary<int, SkillNameEntry>
        {
            [271] = new SkillNameEntry(271, "Icebolt", "冰矛"),
        });
        Assert.Equal("冰矛", resolver.TryResolveSkill(271));
        Assert.Null(resolver.TryResolveSkill(999));
    }
}
