using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Stats;

// 0x520E CreatureBodyUpdate (40 B): body sliders.
// Observed layout: {Long entityId, Float height, Float weight, Float upper, Float lower}.
public sealed record CreatureBodyUpdate(
    ulong EntityId,
    float Height,
    float Weight,
    float Upper,
    float Lower);

public sealed class CreatureBodyUpdateDecoder : IPacketDecoder
{
    public uint Op => 0x0000520E;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        ulong entityId = e.Count > 0 && e[0].Type == MessageElemType.Long ? e[0].AsUInt64() : 0;
        float height = ReadFloat(e, 1);
        float weight = ReadFloat(e, 2);
        float upper  = ReadFloat(e, 3);
        float lower  = ReadFloat(e, 4);
        return new CreatureBodyUpdate(entityId, height, weight, upper, lower);
    }

    private static float ReadFloat(IReadOnlyList<MessageElem> e, int i) =>
        i < e.Count && e[i].Type == MessageElemType.Float ? e[i].AsFloat() : 0f;
}
