using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Pet;

namespace Mabipacade.Decoders.Tests.Pet;

public class PetPrpDetailDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x0000AE1B, new PetPrpDetailDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        // 16 Ints, sample 08-05-26: [5524, 2, 2, 1, 6, 0, 0, 0, 0, 0, 1, 0, 185, 1, 0, 0]
        uint[] raw = { 5524, 2, 2, 1, 6, 0, 0, 0, 0, 0, 1, 0, 185, 1, 0, 0 };
        var elems = new MessageElem[raw.Length];
        for (int i = 0; i < raw.Length; i++)
            elems[i] = MessageElem.Int(raw[i]);
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x0000AE1B, 1UL, elems);
        var result = Assert.IsType<PetPrpDetail>(new PetPrpDetailDecoder().Decode(input));
        Assert.Equal(16, result.Values.Count);
        Assert.Equal(5524u, result.Values[0]);
        Assert.Equal(185u, result.Values[12]);
    }
}
