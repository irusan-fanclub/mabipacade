using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Skills;

public sealed record PlayerSkillStop;

public sealed class PlayerSkillStopDecoder : IPacketDecoder
{
    public uint Op => 0x698B;
    public object Decode(DecoderInput input) => new PlayerSkillStop();
}
