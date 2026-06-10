using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Combat;

public sealed record SetFinisher2;

public sealed class SetFinisher2Decoder : IPacketDecoder
{
    public uint Op => 0x7922;
    public object Decode(DecoderInput input) => new SetFinisher2();
}
