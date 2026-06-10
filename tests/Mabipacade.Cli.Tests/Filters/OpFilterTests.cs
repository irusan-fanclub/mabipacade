using Mabipacade.Cli.Filters;

namespace Mabipacade.Cli.Tests.Filters;

public class OpFilterTests
{
    [Fact]
    public void Empty_AllowsEverything()
    {
        var f = OpFilter.Parse(null);
        Assert.True(f.Allows(0x6984));
        Assert.True(f.Allows(0xFFFF));
    }

    [Fact]
    public void Hex_CommaSeparated_LimitsToList()
    {
        var f = OpFilter.Parse("0x00006984,0x00007926");
        Assert.True(f.Allows(0x6984));
        Assert.True(f.Allows(0x7926));
        Assert.False(f.Allows(0x6985));
    }

    [Fact]
    public void Whitespace_Tolerated()
    {
        var f = OpFilter.Parse("0x6984 , 0x00007926");
        Assert.True(f.Allows(0x6984));
        Assert.True(f.Allows(0x7926));
    }

    [Fact]
    public void InvalidHex_Throws()
    {
        Assert.Throws<FormatException>(() => OpFilter.Parse("0xZZZZ"));
    }
}
