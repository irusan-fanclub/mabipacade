using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Entity;

public sealed record EntityAppear(uint RaceId, string Name);

public sealed class EntityAppearDecoder : IPacketDecoder
{
    public uint Op => 0x0000520C;
    public object Decode(DecoderInput input)
    {
        // Body shape requires the reference impl's parser; M1 emits a
        // minimal placeholder POCO with defaults. Real extraction lands in v2
        // once the reference parser is ported and verified.
        return new EntityAppear(RaceId: 0u, Name: "");
    }
}
