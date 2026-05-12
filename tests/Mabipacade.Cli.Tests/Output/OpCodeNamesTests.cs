using Mabipacade.Cli.Output;

namespace Mabipacade.Cli.Tests.Output;

public class OpCodeNamesTests
{
    [Fact]
    public void Known_Op_ReturnsEnumName()
    {
        Assert.Equal("PlayerSkillPrepareStart", OpCodeNames.TryGetName(0x6984));
        Assert.Equal("EntityAppear", OpCodeNames.TryGetName(0x520C));
        Assert.Equal("CombatActionPack", OpCodeNames.TryGetName(0x7926));
    }

    [Fact]
    public void Unknown_Op_ReturnsNull()
    {
        Assert.Null(OpCodeNames.TryGetName(0xFFFF));
        Assert.Null(OpCodeNames.TryGetName(0x1234));
    }
}
