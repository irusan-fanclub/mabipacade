using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Pet;

// 0xAE1B - PRP detail breakdown. 16 Ints; idx0 is PRP value/cap (e.g. 1740/5524/10000).
public sealed record PetPrpDetail(IReadOnlyList<uint> Values);

public sealed class PetPrpDetailDecoder : IPacketDecoder
{
    public uint Op => 0xAE1B;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        var values = new List<uint>(e.Count);
        for (int i = 0; i < e.Count; i++)
            values.Add(e[i].Type == MessageElemType.Int ? e[i].AsUInt32() : 0);
        return new PetPrpDetail(values);
    }
}
