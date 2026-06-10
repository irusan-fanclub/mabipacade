using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.World;

public sealed record Notice(string Message, uint? Duration);

public sealed class NoticeDecoder : IPacketDecoder
{
    public uint Op => 0x526D;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        string message = "";
        uint? duration = null;
        bool gotMessage = false;
        for (int i = 0; i < e.Count; i++)
        {
            if (!gotMessage && e[i].Type == MessageElemType.String)
            {
                message = e[i].AsString();
                gotMessage = true;
                continue;
            }
            if (gotMessage && duration is null && e[i].Type == MessageElemType.Int)
                duration = e[i].AsUInt32();
        }
        return new Notice(message, duration);
    }
}
