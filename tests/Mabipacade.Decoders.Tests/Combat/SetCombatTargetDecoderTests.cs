using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Combat;

namespace Mabipacade.Decoders.Tests.Combat;

public class SetCombatTargetDecoderTests
{
    [Fact]
    public void Op_Matches() => Assert.Equal((uint)0x7920, new SetCombatTargetDecoder().Op);

    [Fact]
    public void Decodes_Sample()
    {
        var elems = new List<MessageElem> { MessageElem.Long(4503599628111714), MessageElem.Byte(0), MessageElem.String("") };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x7920, 0UL, elems);
        var r = (SetCombatTarget)new SetCombatTargetDecoder().Decode(input);
        Assert.Equal(4503599628111714UL, r.TargetId);
        Assert.Equal((byte)0, r.Unknown);
        Assert.Equal("", r.Extra);
    }

    [Fact]
    public void Handles_Empty() =>
        Assert.Equal(0UL, ((SetCombatTarget)new SetCombatTargetDecoder().Decode(
            new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x7920, 0UL, new List<MessageElem>()))).TargetId);
}
