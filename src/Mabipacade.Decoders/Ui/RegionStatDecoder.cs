using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Ui;

/// <summary>
/// 0xA93B RegionStat — { Int regionId, Int value }. e.g. (35004, 0). Not in Aura.
/// </summary>
public sealed record RegionStat(uint RegionId, uint Value);

public sealed class RegionStatDecoder : IPacketDecoder
{
    public uint Op => 0x0000A93B;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;

        var regionId = e.Count >= 1 && e[0].Type == MessageElemType.Int ? e[0].AsUInt32() : 0u;
        var value = e.Count >= 2 && e[1].Type == MessageElemType.Int ? e[1].AsUInt32() : 0u;

        return new RegionStat(regionId, value);
    }
}
