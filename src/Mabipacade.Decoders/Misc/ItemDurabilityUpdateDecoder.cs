using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Misc;

// 0x5BD5 ItemDurabilityUpdate (35 B): {Long instanceId, Int durability}. (Aura: ItemDurabilityUpdate)
public sealed record ItemDurabilityUpdate(ulong InstanceId, uint Durability);

public sealed class ItemDurabilityUpdateDecoder : IPacketDecoder
{
    public uint Op => 0x00005BD5;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        ulong instanceId = e.Count > 0 && e[0].Type == MessageElemType.Long ? e[0].AsUInt64() : 0;
        uint durability = e.Count > 1 && e[1].Type == MessageElemType.Int ? e[1].AsUInt32() : 0;
        return new ItemDurabilityUpdate(instanceId, durability);
    }
}
