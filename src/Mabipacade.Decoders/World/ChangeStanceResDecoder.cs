using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.World;

public sealed record ChangeStanceRes;

public sealed class ChangeStanceResDecoder : IPacketDecoder
{
    public uint Op => 0x00006E29;
    public object Decode(DecoderInput input) => new ChangeStanceRes();
}
