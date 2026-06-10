using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Pet;

public sealed record SkillInfo(ushort SkillId, byte[] Info);

public sealed class SkillInfoDecoder : IPacketDecoder
{
    public uint Op => 0x6979;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        byte[] bin = e.Count > 0 && e[0].Type == MessageElemType.Bin
            ? e[0].AsBytes()
            : Array.Empty<byte>();
        // First 2 bytes little-endian = SkillId (e.g. 0x52 0xC3 = 50002).
        ushort skillId = bin.Length >= 2 ? (ushort)(bin[0] | bin[1] << 8) : (ushort)0;
        return new SkillInfo(skillId, bin);
    }
}
