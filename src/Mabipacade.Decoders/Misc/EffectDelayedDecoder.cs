using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Misc;

public sealed record EffectDelayed;

public sealed class EffectDelayedDecoder : IPacketDecoder
{
    public uint Op => 0x00009095;
    public object Decode(DecoderInput input) => new EffectDelayed();
}
