using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Skills;

public sealed record PlayerSkillPostCastAck2(ushort SkillId);

public sealed class PlayerSkillPostCastAck2Decoder : IPacketDecoder
{
    public ushort Op => 0x6989;
    public object Decode(DecoderInput input) =>
        new PlayerSkillPostCastAck2(input.Elems[0].AsUInt16());
}
