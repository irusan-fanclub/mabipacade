using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Misc;

// 0x59EE ItemUpdate (38-40 B): {Long instanceId, Short, Int} - quantity/durability change.
public sealed record ItemUpdate(ulong InstanceId, ushort Short, uint Int);

public sealed class ItemUpdateDecoder : IPacketDecoder
{
    public uint Op => 0x59EE;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        ulong instanceId = e.Count > 0 && e[0].Type == MessageElemType.Long ? e[0].AsUInt64() : 0;
        ushort s = e.Count > 1 && e[1].Type == MessageElemType.Short ? e[1].AsUInt16() : (ushort)0;
        uint i = e.Count > 2 && e[2].Type == MessageElemType.Int ? e[2].AsUInt32() : 0;
        return new ItemUpdate(instanceId, s, i);
    }
}
