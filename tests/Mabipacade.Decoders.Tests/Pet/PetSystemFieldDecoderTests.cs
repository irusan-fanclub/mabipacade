using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Pet;

namespace Mabipacade.Decoders.Tests.Pet;

public class PetSystemFieldDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x0000909A, new PetSystemFieldDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        // 18-elem stub: idx5 Byte = 1 (enable), idx10 Byte = 1 (varying), rest 0.
        var elems = new[]
        {
            MessageElem.Byte(0),  // 0
            MessageElem.Int(0),   // 1
            MessageElem.Byte(0),  // 2
            MessageElem.Int(0),   // 3
            MessageElem.Byte(0),  // 4
            MessageElem.Byte(1),  // 5 -> Flag1
            MessageElem.Byte(0),  // 6
            MessageElem.Long(0),  // 7
            MessageElem.Long(0),  // 8
            MessageElem.Int(0),   // 9
            MessageElem.Byte(1),  // 10 -> Flag2
            MessageElem.Byte(0),  // 11
            MessageElem.Int(0),   // 12
            MessageElem.Int(0),   // 13
            MessageElem.Int(0),   // 14
            MessageElem.Int(0),   // 15
            MessageElem.Byte(0),  // 16
            MessageElem.Byte(0),  // 17
        };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x0000909A, 1UL, elems);
        var result = Assert.IsType<PetSystemField>(new PetSystemFieldDecoder().Decode(input));
        Assert.Equal((byte)1, result.Flag1);
        Assert.Equal((byte)1, result.Flag2);
    }
}
