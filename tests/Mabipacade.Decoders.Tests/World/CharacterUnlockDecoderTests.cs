using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.World;

namespace Mabipacade.Decoders.Tests.World;

public class CharacterUnlockDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x701F, new CharacterUnlockDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        // raw 0x701F: [Int 0xEFFFFFFE]
        var elems = new[] { MessageElem.Int(0xEFFFFFFE) };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x701F, 0UL, elems);
        var result = Assert.IsType<CharacterUnlock>(new CharacterUnlockDecoder().Decode(input));
        Assert.Equal(0xEFFFFFFEu, result.Marker);
    }
}
