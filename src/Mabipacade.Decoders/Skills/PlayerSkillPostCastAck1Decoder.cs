using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Skills;

public sealed record PlayerSkillPostCastAck1(ushort SkillId);

public sealed class PlayerSkillPostCastAck1Decoder : IPacketDecoder
{
    public uint Op => 0x00006988;
    public object Decode(DecoderInput input) =>
        new PlayerSkillPostCastAck1(input.Elems[0].AsUInt16());
}
