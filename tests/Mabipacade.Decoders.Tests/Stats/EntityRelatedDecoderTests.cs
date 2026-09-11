using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Stats;

namespace Mabipacade.Decoders.Tests.Stats;

public class EntityRelatedDecoderTests
{
    [Fact]
    public void Op_Matches() => Assert.Equal((uint)0x00007534, new EntityRelatedDecoder().Op);

    [Fact]
    public void Decodes_CapturesBytes()
    {
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x00007534, 0UL,
            new List<MessageElem>
            {
                MessageElem.Byte(0), MessageElem.Byte(1), MessageElem.Byte(2),
            });

        var r = (EntityRelated)new EntityRelatedDecoder().Decode(input);

        Assert.Equal(new byte[] { 0, 1, 2 }, r.Bytes);
    }
}
