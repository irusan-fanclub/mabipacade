using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Skills;

public sealed record PlayerSkillPrepareReady(ushort SkillId);

public sealed class PlayerSkillPrepareReadyDecoder : IPacketDecoder
{
    public uint Op => 0x6985;
    public object Decode(DecoderInput input) =>
        new PlayerSkillPrepareReady(input.Elems[0].AsUInt16());
}
