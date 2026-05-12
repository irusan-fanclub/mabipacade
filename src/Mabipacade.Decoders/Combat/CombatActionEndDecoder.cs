using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Combat;

public sealed record CombatActionEnd;

public sealed class CombatActionEndDecoder : IPacketDecoder
{
    public ushort Op => 0x7925;
    public object Decode(DecoderInput input) => new CombatActionEnd();
}
