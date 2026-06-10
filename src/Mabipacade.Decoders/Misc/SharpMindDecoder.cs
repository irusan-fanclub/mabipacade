using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Misc;

public sealed record SharpMind;

public sealed class SharpMindDecoder : IPacketDecoder
{
    public uint Op => 0xA41E;
    public object Decode(DecoderInput input) => new SharpMind();
}
