using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Stats;

public sealed record ConditionUpdate2;

public sealed class ConditionUpdate2Decoder : IPacketDecoder
{
    public uint Op => 0xA028;
    public object Decode(DecoderInput input) => new ConditionUpdate2();
}
