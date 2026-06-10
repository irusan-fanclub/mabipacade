using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.World;

public sealed record RemoveDynamicRegion(uint RegionId);

public sealed class RemoveDynamicRegionDecoder : IPacketDecoder
{
    public uint Op => 0x9572;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        uint regionId = e.Count > 0 && e[0].Type == MessageElemType.Int ? e[0].AsUInt32() : 0;
        return new RemoveDynamicRegion(regionId);
    }
}
