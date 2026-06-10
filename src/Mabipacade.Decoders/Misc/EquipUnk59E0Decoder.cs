using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Misc;

// 0x59E0 (295-1947 B): large item-ish payload. Capture leading instanceId and
// the elem count only; full structure is not yet decoded.
public sealed record EquipUnk59E0(ulong InstanceId, int ElemCount);

public sealed class EquipUnk59E0Decoder : IPacketDecoder
{
    public uint Op => 0x59E0;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        ulong instanceId = e.Count > 0 && e[0].Type == MessageElemType.Long ? e[0].AsUInt64() : 0;
        return new EquipUnk59E0(instanceId, e.Count);
    }
}
