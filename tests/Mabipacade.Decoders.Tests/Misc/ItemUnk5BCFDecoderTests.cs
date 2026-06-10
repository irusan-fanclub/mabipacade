using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Misc;

namespace Mabipacade.Decoders.Tests.Misc;

public class ItemUnk5BCFDecoderTests
{
    [Fact]
    public void Op_Matches() => Assert.Equal((uint)0x5BCF, new ItemUnk5BCFDecoder().Op);

    [Fact]
    public void Decodes_Sample()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x5BCF, 0UL,
            new List<MessageElem> { MessageElem.Byte(1) });
        var r = (ItemUnk5BCF)new ItemUnk5BCFDecoder().Decode(input);
        Assert.Equal((byte)1, r.Flag);
    }
}
