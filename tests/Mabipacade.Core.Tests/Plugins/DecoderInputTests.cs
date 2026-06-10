using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Core.Tests.Plugins;

public class DecoderInputTests
{
    [Fact]
    public void Construct_PreservesAllFields()
    {
        var ts = DateTime.UtcNow;
        var elems = new[] { MessageElem.Short(1) };
        var input = new DecoderInput(ts, Direction.Inbound, 0x520C, 1UL, elems);

        Assert.Equal(ts, input.TimestampUtc);
        Assert.Equal((uint)0x520C, input.Op);
        Assert.Equal(1UL, input.EntityId);
        Assert.Same(elems, input.Elems);
    }
}
