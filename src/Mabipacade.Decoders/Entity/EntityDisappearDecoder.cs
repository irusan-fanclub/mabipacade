using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Entity;

public sealed record EntityDisappear;

public sealed class EntityDisappearDecoder : IPacketDecoder
{
    public uint Op => 0x0000520D;
    public object Decode(DecoderInput input) => new EntityDisappear();
}
