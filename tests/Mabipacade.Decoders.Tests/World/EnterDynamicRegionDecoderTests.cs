using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;
using Mabipacade.Decoders.World;

namespace Mabipacade.Decoders.Tests.World;

public class EnterDynamicRegionDecoderTests
{
    [Fact]
    public void Op_Matches()
    {
        Assert.Equal((uint)0x9571, new EnterDynamicRegionDecoder().Op);
    }

    [Fact]
    public void Decodes_Sample()
    {
        // 14-elem pet farm sample (Farm_Desert_90, dynamic region 35004)
        var elems = new[]
        {
            MessageElem.Long(4504699139850743UL), // [0] pet eid
            MessageElem.Int(0),                    // [1] warpFromRegionId
            MessageElem.Int(35004),                // [2] regionId
            MessageElem.String("DynamicRegion35004"), // [3] regionName
            MessageElem.Int(0x80000004),           // [4] flag
            MessageElem.Int(5054),                 // [5] baseId
            MessageElem.String("Farm_Desert_90"),  // [6] worldName
            MessageElem.Int(200),                  // [7]
            MessageElem.Byte(0),                   // [8]
            MessageElem.String("data/world/Farm_Desert_90/"), // [9] dataPath
            MessageElem.Byte(0),                   // [10]
            MessageElem.Byte(0),                   // [11]
            MessageElem.Int(43147),                // [12] posX
            MessageElem.Int(39954),                // [13] posY
        };
        var input = new DecoderInput(DateTime.UtcNow, Direction.Inbound, 0x9571, 3458764513820540928UL, elems);
        var result = Assert.IsType<EnterDynamicRegion>(new EnterDynamicRegionDecoder().Decode(input));
        Assert.Equal((uint)35004, result.RegionId);
        Assert.Equal("DynamicRegion35004", result.RegionName);
        Assert.Equal("Farm_Desert_90", result.WorldName);
        Assert.Equal((uint)43147, result.PosX);
        Assert.Equal((uint)39954, result.PosY);
    }
}
