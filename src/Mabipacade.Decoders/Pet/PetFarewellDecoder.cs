using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Pet;

// 0xAD04 - empty body. Fires after 0x520D EntityDisappear on pet unsummon.
public sealed record PetFarewell;

public sealed class PetFarewellDecoder : IPacketDecoder
{
    public uint Op => 0xAD04;

    public object Decode(DecoderInput input) => new PetFarewell();
}
