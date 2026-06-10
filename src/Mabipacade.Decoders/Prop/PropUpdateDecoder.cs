using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Prop;

/// <summary>
/// 0x52D2 PropUpdate — routed by prop EID itself. Body: { String state, Long ts(0),
/// Byte hasXml, Float direction, Short 0 }. We capture state + direction (radians).
/// </summary>
public sealed record PropUpdate(string State, float Direction);

public sealed class PropUpdateDecoder : IPacketDecoder
{
    public uint Op => 0x000052D2;

    public object Decode(DecoderInput input)
    {
        var state = "";
        float direction = 0f;

        foreach (var el in input.Elems)
        {
            switch (el.Type)
            {
                case MessageElemType.String when state.Length == 0:
                    state = el.AsString();
                    break;
                case MessageElemType.Float:
                    direction = el.AsFloat();
                    break;
            }
        }

        return new PropUpdate(state, direction);
    }
}
