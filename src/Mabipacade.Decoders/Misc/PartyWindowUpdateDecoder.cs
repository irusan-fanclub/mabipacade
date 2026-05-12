using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Misc;

public sealed record PartyWindowUpdate;

public sealed class PartyWindowUpdateDecoder : IPacketDecoder
{
    public ushort Op => 0xA43C;
    public object Decode(DecoderInput input) => new PartyWindowUpdate();
}
