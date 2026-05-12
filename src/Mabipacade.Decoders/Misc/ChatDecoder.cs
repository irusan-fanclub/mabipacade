using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Misc;

public sealed record Chat(string Sender, string Message);

public sealed class ChatDecoder : IPacketDecoder
{
    public ushort Op => 0x526C;
    public object Decode(DecoderInput input)
    {
        // Body shape speculative: looks for [String, String] pattern; fallback
        // to empty strings if missing.
        string sender = "";
        string message = "";
        if (input.Elems.Count >= 1 && input.Elems[0].Type == MessageElemType.String)
            sender = input.Elems[0].AsString();
        if (input.Elems.Count >= 2 && input.Elems[1].Type == MessageElemType.String)
            message = input.Elems[1].AsString();
        return new Chat(sender, message);
    }
}
