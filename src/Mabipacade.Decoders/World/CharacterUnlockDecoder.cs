using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.World;

public sealed record CharacterUnlock(uint Marker);

public sealed class CharacterUnlockDecoder : IPacketDecoder
{
    public uint Op => 0x0000701F;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        uint marker = e.Count > 0 && e[0].Type == MessageElemType.Int ? e[0].AsUInt32() : 0;
        return new CharacterUnlock(marker);
    }
}
