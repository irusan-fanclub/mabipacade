using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Pet;

// These opcodes carry a non-zero "category" byte in the upper 16 bits of the
// 32-bit op (so they must be matched on the full value, not a truncated
// ushort). cat 2 = buff/KV state, cat 1 = pet collection/resource.

// 0x00021208: buff / state KV channel. {Byte 2, Short 1, Byte addRemove,
// String name, [Byte type, value...]}. name examples: SZDA, MECSDAPBMV, MEBL.
public sealed record BuffStateUpdate(bool IsAdd, string Name);

public sealed class BuffStateUpdateDecoder : IPacketDecoder
{
    public uint Op => 0x00021208;
    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        // elem[2] is the add/remove flag (0 = remove, 1 = add); elem[3] the name.
        bool isAdd = e.Count > 2 && e[2].Type == MessageElemType.Byte && e[2].AsByte() != 0;
        string name = "";
        for (int i = 0; i < e.Count; i++)
            if (e[i].Type == MessageElemType.String) { name = e[i].AsString(); break; }
        return new BuffStateUpdate(isAdd, name);
    }
}

// 0x00020F6F: pet capacity info. {Long 0, Int -1, Int 0, Int slots?, Int max?}.
public sealed record PetCapacity(uint A, uint B, uint C, uint D);

public sealed class PetCapacityDecoder : IPacketDecoder
{
    public uint Op => 0x00020F6F;
    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        uint Int(int i) => i < e.Count && e[i].Type == MessageElemType.Int ? e[i].AsUInt32() : 0;
        // elem[0] is a Long; the four ints follow.
        return new PetCapacity(Int(1), Int(2), Int(3), Int(4));
    }
}

// 0x0001FBD4: pet resource sync batch begin. {Int 0, Int 1, Long petEid,
// Int 32, Byte, Byte, Byte}.
public sealed record PetResourceSync(ulong PetId, uint Resource);

public sealed class PetResourceSyncDecoder : IPacketDecoder
{
    public uint Op => 0x0001FBD4;
    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        ulong pet = e.Count > 2 && e[2].Type == MessageElemType.Long ? e[2].AsUInt64() : 0;
        uint res = e.Count > 3 && e[3].Type == MessageElemType.Int ? e[3].AsUInt32() : 0;
        return new PetResourceSync(pet, res);
    }
}
