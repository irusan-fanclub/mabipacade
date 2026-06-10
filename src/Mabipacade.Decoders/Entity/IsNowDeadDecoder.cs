using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Entity;

public sealed record IsNowDead;

public sealed class IsNowDeadDecoder : IPacketDecoder
{
    public uint Op => 0x000053FC;
    public object Decode(DecoderInput input) => new IsNowDead();
}
