using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Stats;

public sealed record StatUpdatePrivate(IReadOnlyList<(uint StatId, double Value)> Stats);

public sealed class StatUpdatePrivateDecoder : IPacketDecoder
{
    public uint Op => 0x00007530;

    public object Decode(DecoderInput input) =>
        new StatUpdatePrivate(StatUpdateParser.Parse(input.Elems));
}
