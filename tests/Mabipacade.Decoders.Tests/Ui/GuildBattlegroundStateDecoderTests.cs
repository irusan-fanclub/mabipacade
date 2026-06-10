using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Ui;

namespace Mabipacade.Decoders.Tests.Ui;

public class GuildBattlegroundStateDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0xA90E, new GuildBattlegroundStateDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0xA90E, 0UL,
            new[]
            {
                MessageElem.String("Senmag_Guild_BattleGround"),
                MessageElem.Byte(1),
                MessageElem.String("closed"),
                MessageElem.Byte(1),
            });

        var result = (GuildBattlegroundState)new GuildBattlegroundStateDecoder().Decode(input);

        Assert.Equal("Senmag_Guild_BattleGround", result.Name);
        Assert.Equal((byte)1, result.Flag1);
        Assert.Equal("closed", result.State);
        Assert.Equal((byte)1, result.Flag2);
    }
}
