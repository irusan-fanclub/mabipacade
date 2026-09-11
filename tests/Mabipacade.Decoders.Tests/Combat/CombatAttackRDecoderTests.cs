using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Combat;

namespace Mabipacade.Decoders.Tests.Combat;

public class CombatAttackRDecoderTests
{
    [Fact]
    public void Op_Matches() => Assert.Equal((uint)0x00007D01, new CombatAttackRDecoder().Op);

    [Fact]
    public void Decodes_Sample()
    {
        var elems = new List<MessageElem> { MessageElem.Byte(1) };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x00007D01, 0UL, elems);
        var r = (CombatAttackR)new CombatAttackRDecoder().Decode(input);
        Assert.Equal((byte)1, r.Result);
    }
}
