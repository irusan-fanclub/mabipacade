using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Pet;

public sealed record GetPetAiR(byte HasAi, string? AiFile);

public sealed class GetPetAiRDecoder : IPacketDecoder
{
    public uint Op => 0xA8A3;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        byte hasAi = e.Count > 0 && e[0].Type == MessageElemType.Byte ? e[0].AsByte() : (byte)0;
        // String only present when HasAi != 0 (TW omits the String elem on negative answer).
        string? aiFile = hasAi != 0 && e.Count > 1 && e[1].Type == MessageElemType.String
            ? e[1].AsString()
            : null;
        return new GetPetAiR(hasAi, aiFile);
    }
}
