using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Combat;

namespace Mabipacade.Decoders.Tests.Combat;

public class SetFinisher2DecoderTests
{
    [Fact]
    public void Op_Matches() => Assert.Equal((uint)0x00007922, new SetFinisher2Decoder().Op);

    [Fact]
    public void Decodes_Empty()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x00007922, 0UL, new List<MessageElem>());
        var r = (SetFinisher2)new SetFinisher2Decoder().Decode(input);
        Assert.NotNull(r);
    }
}
