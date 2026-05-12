using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Entity;

public sealed record EntityDisappear;

public sealed class EntityDisappearDecoder : IPacketDecoder
{
    public ushort Op => 0x520D;
    public object Decode(DecoderInput input) => new EntityDisappear();
}
