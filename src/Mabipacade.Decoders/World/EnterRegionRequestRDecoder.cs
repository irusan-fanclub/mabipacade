using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.World;

public sealed record EnterRegionRequestR(byte Success, ulong EntityId, ulong FileTime);

public sealed class EnterRegionRequestRDecoder : IPacketDecoder
{
    public uint Op => 0x659C;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        byte success = e.Count > 0 && e[0].Type == MessageElemType.Byte ? e[0].AsByte() : (byte)0;
        ulong entityId = e.Count > 1 && e[1].Type == MessageElemType.Long ? e[1].AsUInt64() : 0;
        ulong fileTime = e.Count > 2 && e[2].Type == MessageElemType.Long ? e[2].AsUInt64() : 0;
        return new EnterRegionRequestR(success, entityId, fileTime);
    }
}
