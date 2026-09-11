using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Combat;

public sealed record RemoveDeathScreen;

public sealed class RemoveDeathScreenDecoder : IPacketDecoder
{
    public uint Op => 0x000053FD;
    public object Decode(DecoderInput input) => new RemoveDeathScreen();
}
