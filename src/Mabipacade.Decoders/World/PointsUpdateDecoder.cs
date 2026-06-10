using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.World;

public sealed record PointsUpdate(uint Points);

public sealed class PointsUpdateDecoder : IPacketDecoder
{
    public uint Op => 0x4E90;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        uint points = 0;
        for (int i = 0; i < e.Count; i++)
            if (e[i].Type == MessageElemType.Int) { points = e[i].AsUInt32(); break; }
        return new PointsUpdate(points);
    }
}
