using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Skills;

public sealed record PlayerSkillPrepareProgress(ushort SkillId);

public sealed class PlayerSkillPrepareProgressDecoder : IPacketDecoder
{
    public uint Op => 0x6993;
    // SkillId is at msg[2], not msg[0] — protocol quirk documented in
    // mabinogi-packet-decoding/README.md (section "Player skill chain").
    public object Decode(DecoderInput input) =>
        new PlayerSkillPrepareProgress(input.Elems[2].AsUInt16());
}
