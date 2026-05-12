using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Stats;

public sealed record StatUpdatePublic;

public sealed class StatUpdatePublicDecoder : IPacketDecoder
{
    public ushort Op => 0x7532;
    public object Decode(DecoderInput input) => new StatUpdatePublic();
}
