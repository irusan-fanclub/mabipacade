using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Pet;

namespace Mabipacade.Decoders.Tests.Pet;

public class GetPetAiRDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x0000A8A3, new GetPetAiRDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        var elems = new[]
        {
            MessageElem.Byte(1),
            MessageElem.String("OasisRuleSubmission.xml"),
        };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x0000A8A3, 4504699139850743UL, elems);
        var result = Assert.IsType<GetPetAiR>(new GetPetAiRDecoder().Decode(input));
        Assert.Equal((byte)1, result.HasAi);
        Assert.Equal("OasisRuleSubmission.xml", result.AiFile);
    }

    [Fact]
    public void Decodes_NegativeAnswerOmitsString()
    {
        var elems = new[] { MessageElem.Byte(0) };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x0000A8A3, 4504699144746549UL, elems);
        var result = Assert.IsType<GetPetAiR>(new GetPetAiRDecoder().Decode(input));
        Assert.Equal((byte)0, result.HasAi);
        Assert.Null(result.AiFile);
    }
}
