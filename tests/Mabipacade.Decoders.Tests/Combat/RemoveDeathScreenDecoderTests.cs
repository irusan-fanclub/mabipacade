using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Combat;

namespace Mabipacade.Decoders.Tests.Combat;

public class RemoveDeathScreenDecoderTests
{
    [Fact]
    public void Op_Matches() => Assert.Equal((uint)0x000053FD, new RemoveDeathScreenDecoder().Op);

    [Fact]
    public void Decodes_Empty()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x000053FD, 0UL, new List<MessageElem>());
        var r = (RemoveDeathScreen)new RemoveDeathScreenDecoder().Decode(input);
        Assert.NotNull(r);
    }
}
