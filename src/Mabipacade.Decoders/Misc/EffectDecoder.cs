using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Misc;

public sealed record Effect;

public sealed class EffectDecoder : IPacketDecoder
{
    public uint Op => 0x9091;
    public object Decode(DecoderInput input) => new Effect();
}
