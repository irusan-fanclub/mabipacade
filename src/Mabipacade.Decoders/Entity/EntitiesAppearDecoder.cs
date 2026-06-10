using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Entity;

public sealed record EntitiesAppear;

public sealed class EntitiesAppearDecoder : IPacketDecoder
{
    public uint Op => 0x5334;
    public object Decode(DecoderInput input) => new EntitiesAppear();
}
