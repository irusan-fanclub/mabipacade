using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Skills;

public sealed record PlayerSkillPrepareStart(ushort SkillId);

public sealed class PlayerSkillPrepareStartDecoder : IPacketDecoder
{
    public uint Op => 0x00006984;
    public object Decode(DecoderInput input) =>
        new PlayerSkillPrepareStart(input.Elems[0].AsUInt16());
}
