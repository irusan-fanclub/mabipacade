using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.World;

public sealed record Disappear(ulong EntityId);

public sealed class DisappearDecoder : IPacketDecoder
{
    public uint Op => 0x4E2A;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        ulong entityId = e.Count > 0 && e[0].Type == MessageElemType.Long ? e[0].AsUInt64() : 0;
        return new Disappear(entityId);
    }
}
