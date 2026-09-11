namespace Mabipacade.Core.Plugins;

public interface IPacketDecoder
{
    uint Op { get; }
    object Decode(DecoderInput input);
}
