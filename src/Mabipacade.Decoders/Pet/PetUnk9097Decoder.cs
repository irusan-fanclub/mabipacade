using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Pet;

// 0x9097 - single Long, unknown semantics.
public sealed record PetUnk9097(ulong Value);

public sealed class PetUnk9097Decoder : IPacketDecoder
{
    public uint Op => 0x00009097;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        ulong value = e.Count > 0 && e[0].Type == MessageElemType.Long ? e[0].AsUInt64() : 0;
        return new PetUnk9097(value);
    }
}
