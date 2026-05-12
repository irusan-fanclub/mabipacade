namespace Mabipacade.Core.Plugins;

public interface IPacketDecoder
{
    ushort Op { get; }
    object Decode(DecoderInput input);
}
