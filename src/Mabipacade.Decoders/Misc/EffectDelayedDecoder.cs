using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Misc;

public sealed record EffectDelayed;

public sealed class EffectDelayedDecoder : IPacketDecoder
{
    public ushort Op => 0x9095;
    public object Decode(DecoderInput input) => new EffectDelayed();
}
