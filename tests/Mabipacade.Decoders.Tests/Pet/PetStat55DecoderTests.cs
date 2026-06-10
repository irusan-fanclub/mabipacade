using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Pet;

namespace Mabipacade.Decoders.Tests.Pet;

public class PetStat55DecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0xA032, new PetStat55Decoder().Op);
    }

    [Fact]
    public void Decodes_ModeA_NoEntries()
    {
        var elems = new[] { MessageElem.Int(55), MessageElem.Int(0) };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0xA032, 4504699139850743UL, elems);
        var result = Assert.IsType<PetStat55>(new PetStat55Decoder().Decode(input));
        Assert.Equal(55, result.GroupId);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public void Decodes_ModeB_Sample()
    {
        // [55, 3, 0, 300.0, 0, 1, 400.0, 0, 2, 300.0, 0]
        var elems = new[]
        {
            MessageElem.Int(55), MessageElem.Int(3),
            MessageElem.Int(0), MessageElem.Float(300.0f), MessageElem.Byte(0),
            MessageElem.Int(1), MessageElem.Float(400.0f), MessageElem.Byte(0),
            MessageElem.Int(2), MessageElem.Float(300.0f), MessageElem.Byte(0),
        };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0xA032, 4504699144649501UL, elems);
        var result = Assert.IsType<PetStat55>(new PetStat55Decoder().Decode(input));
        Assert.Equal(55, result.GroupId);
        Assert.Equal(3, result.Entries.Count);
        Assert.Equal((0, 300.0f), result.Entries[0]);
        Assert.Equal((1, 400.0f), result.Entries[1]);
        Assert.Equal((2, 300.0f), result.Entries[2]);
    }
}
