using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Combat;

public sealed record CombatAction;

public sealed class CombatActionDecoder : IPacketDecoder
{
    public uint Op => 0x00007924;
    public object Decode(DecoderInput input) => new CombatAction();
}
