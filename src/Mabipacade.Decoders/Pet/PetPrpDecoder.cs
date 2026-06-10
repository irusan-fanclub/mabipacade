using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Pet;

// 0xAE0F - pet PRP (rebirth point) summary. {Byte mode(3), Int prp, Int stage, Int 0, Int 0}.
public sealed record PetPrp(uint Prp, uint Stage, uint C, uint D);

public sealed class PetPrpDecoder : IPacketDecoder
{
    public uint Op => 0xAE0F;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        uint prp = e.Count > 1 && e[1].Type == MessageElemType.Int ? e[1].AsUInt32() : 0;
        uint stage = e.Count > 2 && e[2].Type == MessageElemType.Int ? e[2].AsUInt32() : 0;
        uint c = e.Count > 3 && e[3].Type == MessageElemType.Int ? e[3].AsUInt32() : 0;
        uint d = e.Count > 4 && e[4].Type == MessageElemType.Int ? e[4].AsUInt32() : 0;
        return new PetPrp(prp, stage, c, d);
    }
}
