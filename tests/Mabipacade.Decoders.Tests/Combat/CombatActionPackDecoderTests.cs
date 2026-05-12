using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.Combat;

namespace Mabipacade.Decoders.Tests.Combat;

public class CombatActionPackDecoderTests
{
    [Fact]
    public void Decode_PreservesEntityId_AsAttackerId()
    {
        var decoder = new CombatActionPackDecoder();
        ulong entityId = 987654321UL;
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x7926, entityId,
            Array.Empty<MessageElem>());
        var result = (CombatActionPack)decoder.Decode(input);
        Assert.Equal(entityId, result.AttackerId);
    }

    [Fact]
    public void Decode_ReturnsEmptySubList()
    {
        var decoder = new CombatActionPackDecoder();
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x7926, 1UL,
            Array.Empty<MessageElem>());
        var result = (CombatActionPack)decoder.Decode(input);
        Assert.Empty(result.Sub);
    }

    [Fact]
    public void Op_Is7926()
    {
        Assert.Equal((ushort)0x7926, new CombatActionPackDecoder().Op);
    }
}
