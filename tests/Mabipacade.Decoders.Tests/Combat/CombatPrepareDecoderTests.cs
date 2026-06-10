using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Combat;

namespace Mabipacade.Decoders.Tests.Combat;

public class CombatPrepareDecoderTests
{
    [Fact]
    public void Op_Matches() => Assert.Equal((uint)0x7919, new CombatPrepareDecoder().Op);

    [Fact]
    public void Decodes_Sample()
    {
        var elems = new List<MessageElem> { MessageElem.Byte(1), MessageElem.Long(4767482419982230) };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x7919, 0UL, elems);
        var r = (CombatPrepare)new CombatPrepareDecoder().Decode(input);
        Assert.Equal((byte)1, r.Flag);
        Assert.Equal(4767482419982230UL, r.TargetId);
    }
}
