using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.World;

public sealed record EnterDynamicRegion(
    uint RegionId,
    string RegionName,
    string WorldName,
    uint PosX,
    uint PosY);

public sealed class EnterDynamicRegionDecoder : IPacketDecoder
{
    public uint Op => 0x9571;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        // Layout: [0]Long eid [1]Int 0 [2]Int regionId [3]String regionName
        //         [4]Int flag [5]Int baseId [6]String worldName [7]Int [8]Byte
        //         [9]String dataPath [10]Byte [11]Byte [12]Int posX [13]Int posY
        uint regionId = e.Count > 2 && e[2].Type == MessageElemType.Int ? e[2].AsUInt32() : 0;
        string regionName = e.Count > 3 && e[3].Type == MessageElemType.String ? e[3].AsString() : "";
        string worldName = e.Count > 6 && e[6].Type == MessageElemType.String ? e[6].AsString() : "";
        uint posX = e.Count > 12 && e[12].Type == MessageElemType.Int ? e[12].AsUInt32() : 0;
        uint posY = e.Count > 13 && e[13].Type == MessageElemType.Int ? e[13].AsUInt32() : 0;
        return new EnterDynamicRegion(regionId, regionName, worldName, posX, posY);
    }
}
