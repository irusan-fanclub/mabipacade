using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Misc;

// 0x59E1 (35 B): observed {Long instanceId, Int value}.
public sealed record EquipUnk59E1(ulong InstanceId, uint Value);

public sealed class EquipUnk59E1Decoder : IPacketDecoder
{
    public uint Op => 0x59E1;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        ulong instanceId = e.Count > 0 && e[0].Type == MessageElemType.Long ? e[0].AsUInt64() : 0;
        uint value = e.Count > 1 && e[1].Type == MessageElemType.Int ? e[1].AsUInt32() : 0;
        return new EquipUnk59E1(instanceId, value);
    }
}
