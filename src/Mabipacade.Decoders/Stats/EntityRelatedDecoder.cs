using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Stats;

public sealed record EntityRelated(IReadOnlyList<byte> Bytes);

public sealed class EntityRelatedDecoder : IPacketDecoder
{
    public uint Op => 0x00007534;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        var bytes = new List<byte>(e.Count);
        foreach (var elem in e)
        {
            if (elem.Type != MessageElemType.Byte) break;
            bytes.Add(elem.AsByte());
        }
        return new EntityRelated(bytes);
    }
}
