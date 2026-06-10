using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Prop;

/// <summary>
/// 0x52D1 PropDisappears — { Long propEid }. No trailing byte (unlike 0x520D).
/// </summary>
public sealed record PropDisappears(ulong PropEid);

public sealed class PropDisappearsDecoder : IPacketDecoder
{
    public uint Op => 0x52D1;

    public object Decode(DecoderInput input)
    {
        var propEid = input.Elems.Count >= 1 && input.Elems[0].Type == MessageElemType.Long
            ? input.Elems[0].AsUInt64()
            : 0UL;
        return new PropDisappears(propEid);
    }
}
