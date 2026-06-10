using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.World;

namespace Mabipacade.Decoders.Tests.World;

public class ChangeStanceResDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x6E29, new ChangeStanceResDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x6E29, 4504699144649501UL,
            Array.Empty<MessageElem>());
        Assert.IsType<ChangeStanceRes>(new ChangeStanceResDecoder().Decode(input));
    }
}
