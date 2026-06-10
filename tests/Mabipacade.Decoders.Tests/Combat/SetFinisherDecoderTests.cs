using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Combat;

namespace Mabipacade.Decoders.Tests.Combat;

public class SetFinisherDecoderTests
{
    [Fact]
    public void Op_Matches() => Assert.Equal((uint)0x7921, new SetFinisherDecoder().Op);

    [Fact]
    public void Decodes_Target()
    {
        var elems = new List<MessageElem> { MessageElem.Long(4767482419982230) };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x7921, 0UL, elems);
        var r = (SetFinisher)new SetFinisherDecoder().Decode(input);
        Assert.Equal(4767482419982230UL, r.TargetId);
    }

    [Fact]
    public void Handles_Empty() =>
        Assert.Equal(0UL, ((SetFinisher)new SetFinisherDecoder().Decode(
            new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x7921, 0UL, new List<MessageElem>()))).TargetId);
}
