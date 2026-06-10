using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Combat;

namespace Mabipacade.Decoders.Tests.Combat;

public class CombatActionEndDecoderTests
{
    [Fact]
    public void Decode_ReturnsCombatActionEnd()
    {
        var decoder = new CombatActionEndDecoder();
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x7925, 99UL,
            Array.Empty<MessageElem>());
        var result = decoder.Decode(input);
        Assert.IsType<CombatActionEnd>(result);
    }

    [Fact]
    public void Op_Is7925()
    {
        Assert.Equal((uint)0x7925, new CombatActionEndDecoder().Op);
    }

    [Fact]
    public void Decodes_ActionId()
    {
        var elems = new List<MessageElem> { MessageElem.Int(2930627) };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x7925, 0UL, elems);
        var r = (CombatActionEnd)new CombatActionEndDecoder().Decode(input);
        Assert.Equal(2930627U, r.ActionId);
    }
}
