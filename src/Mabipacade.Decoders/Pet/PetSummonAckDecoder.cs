using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Pet;

// 0x9070 - empty body. Fires ~600ms after GetPetAiR, ACK for summon completion.
public sealed record PetSummonAck;

public sealed class PetSummonAckDecoder : IPacketDecoder
{
    public uint Op => 0x00009070;

    public object Decode(DecoderInput input) => new PetSummonAck();
}
