using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Combat;

public sealed record SetCombatTarget(ulong TargetId, byte Unknown, string Extra);

public sealed class SetCombatTargetDecoder : IPacketDecoder
{
    public uint Op => 0x00007920;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        ulong target = e.Count > 0 && e[0].Type == MessageElemType.Long ? e[0].AsUInt64() : 0;
        byte unk = e.Count > 1 && e[1].Type == MessageElemType.Byte ? e[1].AsByte() : (byte)0;
        string extra = e.Count > 2 && e[2].Type == MessageElemType.String ? e[2].AsString() : "";
        return new SetCombatTarget(target, unk, extra);
    }
}
