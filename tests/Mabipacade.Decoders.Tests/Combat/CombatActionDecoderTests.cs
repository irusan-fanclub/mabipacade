using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Combat;

namespace Mabipacade.Decoders.Tests.Combat;

public class CombatActionDecoderTests
{
    [Fact]
    public void Decode_ReturnsCombatAction()
    {
        var decoder = new CombatActionDecoder();
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x7924, 99UL,
            Array.Empty<MessageElem>());
        var result = decoder.Decode(input);
        Assert.IsType<CombatAction>(result);
    }

    [Fact]
    public void Op_Is7924()
    {
        Assert.Equal((ushort)0x7924, new CombatActionDecoder().Op);
    }
}
