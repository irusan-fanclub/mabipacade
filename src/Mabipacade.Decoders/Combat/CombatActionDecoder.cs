using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Combat;

public sealed record CombatAction;

public sealed class CombatActionDecoder : IPacketDecoder
{
    public ushort Op => 0x7924;
    public object Decode(DecoderInput input) => new CombatAction();
}
