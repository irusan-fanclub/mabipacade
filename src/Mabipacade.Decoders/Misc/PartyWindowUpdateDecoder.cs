using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Misc;

public sealed record PartyWindowUpdate;

public sealed class PartyWindowUpdateDecoder : IPacketDecoder
{
    public uint Op => 0x0000A43C;
    public object Decode(DecoderInput input) => new PartyWindowUpdate();
}
