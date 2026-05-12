using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Entity;

public sealed record IsNowDead;

public sealed class IsNowDeadDecoder : IPacketDecoder
{
    public ushort Op => 0x53FC;
    public object Decode(DecoderInput input) => new IsNowDead();
}
