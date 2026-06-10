using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.World;

public sealed record CharacterLock(uint Marker);

public sealed class CharacterLockDecoder : IPacketDecoder
{
    public uint Op => 0x0000701E;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        uint marker = e.Count > 0 && e[0].Type == MessageElemType.Int ? e[0].AsUInt32() : 0;
        return new CharacterLock(marker);
    }
}
