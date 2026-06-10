using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Stats;

public sealed record EntityRelated;

public sealed class EntityRelatedDecoder : IPacketDecoder
{
    public uint Op => 0x7534;
    public object Decode(DecoderInput input) => new EntityRelated();
}
