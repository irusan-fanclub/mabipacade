using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Movement;

namespace Mabipacade.Decoders.Tests.Movement;

public class MovementDecodersTests
{
    [Fact]
    public void Walking_Op_IsFull32Bit() => Assert.Equal(0x0FD13021u, new WalkingDecoder().Op);

    [Fact]
    public void Running_Op_IsFull32Bit() => Assert.Equal(0x0F44BBA3u, new RunningDecoder().Op);

    [Fact]
    public void Running_Decodes_FromTo()
    {
        var elems = new List<MessageElem>
        {
            MessageElem.Int(5924), MessageElem.Int(4628),
            MessageElem.Int(5820), MessageElem.Int(4490),
            MessageElem.Byte(1), MessageElem.Byte(0),
        };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x0F44BBA3, 0UL, elems);
        var r = (Running)new RunningDecoder().Decode(input);
        Assert.Equal(5924u, r.FromX);
        Assert.Equal(4490u, r.ToY);
    }

    [Fact]
    public void PetMovementSync_Decodes_ActionAndPos()
    {
        var elems = new List<MessageElem>
        {
            MessageElem.Int(29), MessageElem.Int(4052),
            MessageElem.Float(6182), MessageElem.Float(4616), MessageElem.Byte(1),
        };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x00009093, 0UL, elems);
        var r = (PetMovementSync)new PetMovementSyncDecoder().Decode(input);
        Assert.Equal(29u, r.Action);
        Assert.Equal(4052u, r.Region);
        Assert.Equal(6182f, r.X);
    }
}
