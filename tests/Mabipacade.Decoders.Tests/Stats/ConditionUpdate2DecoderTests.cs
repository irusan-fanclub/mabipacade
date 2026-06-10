using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Stats;

namespace Mabipacade.Decoders.Tests.Stats;

public class ConditionUpdate2DecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0xA028, new ConditionUpdate2Decoder().Op);
    }

    [Fact]
    public void Decodes_ReturnsCorrectType()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0xA028, 0UL,
            Array.Empty<MessageElem>());
        Assert.IsType<ConditionUpdate2>(new ConditionUpdate2Decoder().Decode(input));
    }
}
