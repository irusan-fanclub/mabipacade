using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Pet;

// 0xAF10 - empty body lifecycle signal.
public sealed record PetSyncAck;

public sealed class PetSyncAckDecoder : IPacketDecoder
{
    public uint Op => 0xAF10;

    public object Decode(DecoderInput input) => new PetSyncAck();
}
