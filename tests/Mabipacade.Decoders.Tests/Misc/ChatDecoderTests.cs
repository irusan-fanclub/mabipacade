using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Misc;

namespace Mabipacade.Decoders.Tests.Misc;

public class ChatDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x0000526C, new ChatDecoder().Op);
    }

    [Fact]
    public void Decodes_TwoStringElems_ExtractsSenderAndMessage()
    {
        var elems = new List<MessageElem>
        {
            MessageElem.String("Alice"),
            MessageElem.String("Hello!")
        };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x0000526C, 0UL, elems);
        var result = (Chat)new ChatDecoder().Decode(input);
        Assert.Equal("Alice", result.Sender);
        Assert.Equal("Hello!", result.Message);
    }

    [Fact]
    public void Decodes_EmptyElems_FallsBackToEmptyStrings()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x0000526C, 0UL,
            Array.Empty<MessageElem>());
        var result = (Chat)new ChatDecoder().Decode(input);
        Assert.Equal("", result.Sender);
        Assert.Equal("", result.Message);
    }
}
