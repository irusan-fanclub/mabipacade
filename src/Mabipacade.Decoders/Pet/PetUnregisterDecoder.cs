using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Pet;

public sealed record PetUnregister(ulong PetId);

public sealed class PetUnregisterDecoder : IPacketDecoder
{
    public uint Op => 0x9025;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        ulong pet = e.Count > 0 && e[0].Type == MessageElemType.Long ? e[0].AsUInt64() : 0;
        return new PetUnregister(pet);
    }
}
