using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Pet;

// 0x9098 - single Long, unknown semantics.
public sealed record PetUnk9098(ulong Value);

public sealed class PetUnk9098Decoder : IPacketDecoder
{
    public uint Op => 0x00009098;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        ulong value = e.Count > 0 && e[0].Type == MessageElemType.Long ? e[0].AsUInt64() : 0;
        return new PetUnk9098(value);
    }
}
