using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Stats;

public sealed record StatUpdatePublic(IReadOnlyList<(uint StatId, double Value)> Stats);

public sealed class StatUpdatePublicDecoder : IPacketDecoder
{
    public uint Op => 0x7532;

    public object Decode(DecoderInput input) =>
        new StatUpdatePublic(StatUpdateParser.Parse(input.Elems));
}
