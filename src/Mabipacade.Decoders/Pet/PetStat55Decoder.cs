using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Pet;

public sealed record PetStat55(int GroupId, IReadOnlyList<(int SubId, float Value)> Entries);

public sealed class PetStat55Decoder : IPacketDecoder
{
    public uint Op => 0x0000A032;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        int groupId = e.Count > 0 && e[0].Type == MessageElemType.Int ? (int)e[0].AsUInt32() : 0;
        int count = e.Count > 1 && e[1].Type == MessageElemType.Int ? (int)e[1].AsUInt32() : 0;

        var entries = new List<(int SubId, float Value)>();
        // Mode A: {Int group, Int 0} -> no entries.
        // Mode B: {Int group, Int count, then count x (Int subId, Float val, Byte 0)}.
        int idx = 2;
        for (int k = 0; k < count && idx + 1 < e.Count; k++, idx += 3)
        {
            if (e[idx].Type != MessageElemType.Int || e[idx + 1].Type != MessageElemType.Float)
                break;
            entries.Add(((int)e[idx].AsUInt32(), e[idx + 1].AsFloat()));
        }
        return new PetStat55(groupId, entries);
    }
}
