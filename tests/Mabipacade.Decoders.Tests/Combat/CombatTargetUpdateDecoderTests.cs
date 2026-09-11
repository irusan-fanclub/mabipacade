using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Combat;

namespace Mabipacade.Decoders.Tests.Combat;

public class CombatTargetUpdateDecoderTests
{
    [Fact]
    public void Op_Matches() => Assert.Equal((uint)0x0000791A, new CombatTargetUpdateDecoder().Op);

    [Fact]
    public void Decodes_Sample()
    {
        var elems = new List<MessageElem> { MessageElem.Long(0) };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x0000791A, 0UL, elems);
        var r = (CombatTargetUpdate)new CombatTargetUpdateDecoder().Decode(input);
        Assert.Equal(0UL, r.TargetId);
    }
}
