using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.World;

namespace Mabipacade.Decoders.Tests.World;

public class CharacterLockDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x701E, new CharacterLockDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        // raw 0x701E: [Int 0xEFFFFFFE, Int 0]
        var elems = new[]
        {
            MessageElem.Int(0xEFFFFFFE),
            MessageElem.Int(0),
        };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x701E, 0UL, elems);
        var result = Assert.IsType<CharacterLock>(new CharacterLockDecoder().Decode(input));
        Assert.Equal(0xEFFFFFFEu, result.Marker);
    }
}
