using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Stats;

public sealed record StatUpdatePrivate;

public sealed class StatUpdatePrivateDecoder : IPacketDecoder
{
    public uint Op => 0x7530;
    public object Decode(DecoderInput input) => new StatUpdatePrivate();
}
