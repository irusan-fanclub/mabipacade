using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Stats;

public sealed record CharacterConditionUpdate;

public sealed class CharacterConditionUpdateDecoder : IPacketDecoder
{
    public uint Op => 0x0000A028;
    public object Decode(DecoderInput input) => new CharacterConditionUpdate();
}
