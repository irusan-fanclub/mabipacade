using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Combat;

namespace Mabipacade.Decoders.Tests.Combat;

public class CombatSetAimRDecoderTests
{
    [Fact]
    public void Op_Matches() => Assert.Equal((uint)0x791E, new CombatSetAimRDecoder().Op);

    [Fact]
    public void Decodes_Sample()
    {
        var elems = new List<MessageElem> { MessageElem.Byte(0) };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x791E, 0UL, elems);
        var r = (CombatSetAimR)new CombatSetAimRDecoder().Decode(input);
        Assert.Equal((byte)0, r.Flag);
    }
}
