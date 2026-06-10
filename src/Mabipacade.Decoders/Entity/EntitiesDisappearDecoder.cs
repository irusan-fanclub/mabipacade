using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Entity;

public sealed record EntitiesDisappear;

public sealed class EntitiesDisappearDecoder : IPacketDecoder
{
    public uint Op => 0x5335;
    public object Decode(DecoderInput input) => new EntitiesDisappear();
}
