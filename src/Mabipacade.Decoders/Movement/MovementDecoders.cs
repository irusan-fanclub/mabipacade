using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Movement;

// Walking / Running carry the same body: from (x,y) -> to (x,y) plus two flag
// bytes. The opcodes are full 32-bit values (TW): Walking = 0x0FD13021,
// Running = 0x0F44BBA3.
public sealed record Walking(uint FromX, uint FromY, uint ToX, uint ToY);

public sealed class WalkingDecoder : IPacketDecoder
{
    public uint Op => 0x0FD13021;
    public object Decode(DecoderInput input) => MoveParser.Read<Walking>(input.Elems,
        (a, b, c, d) => new Walking(a, b, c, d));
}

public sealed record Running(uint FromX, uint FromY, uint ToX, uint ToY);

public sealed class RunningDecoder : IPacketDecoder
{
    public uint Op => 0x0F44BBA3;
    public object Decode(DecoderInput input) => MoveParser.Read<Running>(input.Elems,
        (a, b, c, d) => new Running(a, b, c, d));
}

internal static class MoveParser
{
    public static T Read<T>(IReadOnlyList<MessageElem> e, Func<uint, uint, uint, uint, T> make)
    {
        uint At(int i) => i < e.Count && e[i].Type == MessageElemType.Int ? e[i].AsUInt32() : 0;
        return make(At(0), At(1), At(2), At(3));
    }
}

// 0x9093: pet/owner movement & action sync. action 19=bind, 29=region+pos,
// 264=skill/prop trigger, 412=condition ack. Layout varies by action; we
// capture the leading action id and, when present, a region + (x,y).
public sealed record PetMovementSync(uint Action, uint Region, float X, float Y);

public sealed class PetMovementSyncDecoder : IPacketDecoder
{
    public uint Op => 0x00009093;
    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        uint action = e.Count > 0 && e[0].Type == MessageElemType.Int ? e[0].AsUInt32() : 0;
        uint region = e.Count > 1 && e[1].Type == MessageElemType.Int ? e[1].AsUInt32() : 0;
        float x = e.Count > 2 && e[2].Type == MessageElemType.Float ? e[2].AsFloat() : 0;
        float y = e.Count > 3 && e[3].Type == MessageElemType.Float ? e[3].AsFloat() : 0;
        return new PetMovementSync(action, region, x, y);
    }
}
