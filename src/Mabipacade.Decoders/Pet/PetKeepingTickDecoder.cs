using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Pet;

// 0x906F - pet keeping heartbeat. {Long slot/pet EID, Long Windows FILETIME}.
public sealed record PetKeepingTick(ulong SlotOrPetEid, ulong Filetime);

public sealed class PetKeepingTickDecoder : IPacketDecoder
{
    public uint Op => 0x906F;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        ulong eid = e.Count > 0 && e[0].Type == MessageElemType.Long ? e[0].AsUInt64() : 0;
        ulong filetime = e.Count > 1 && e[1].Type == MessageElemType.Long ? e[1].AsUInt64() : 0;
        return new PetKeepingTick(eid, filetime);
    }
}
