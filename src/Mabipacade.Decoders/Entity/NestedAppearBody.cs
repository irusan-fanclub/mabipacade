using Mabipacade.Core.Model;
using Mabipacade.Core.Pipeline;

namespace Mabipacade.Decoders.Entity;

/// <summary>
/// Reader for the nested packet bodies that batch opcodes (0x5334 EntitiesAppear,
/// 0x186A6 MissionRoomActors) carry inside Bin elements: a u32 op and u64 id —
/// both zero on the wire — followed by a standard message. Character bodies
/// repeat 0x520C's single-appear layout: { Long id, Byte 5, String name,
/// String, String, Int raceId, … }.
/// </summary>
internal static class NestedAppearBody
{
    /// <summary>0x520C's data-type byte; 5 = public character data.</summary>
    private const byte PublicCharacterData = 5;

    private const int NestedHeaderLength = 12;

    /// <summary>
    /// False when the blob does not parse as a nested body. Name and race are
    /// null for non-character layouts (data-type byte missing or not 5).
    /// </summary>
    internal static bool TryRead(byte[] body, out ulong entityId, out string? name, out uint? raceId)
    {
        entityId = 0UL;
        name = null;
        raceId = null;

        if (body.Length <= NestedHeaderLength ||
            MessageElemReader.TryRead(body.AsSpan(NestedHeaderLength), out var m) != ReadElemsResult.Ok)
            return false;

        if (m.Count > 0 && m[0].Type == MessageElemType.Long)
            entityId = m[0].AsUInt64();

        if (m.Count > 5 &&
            m[1].Type == MessageElemType.Byte && m[1].AsByte() == PublicCharacterData &&
            m[2].Type == MessageElemType.String)
        {
            name = m[2].AsString();
            if (m[5].Type == MessageElemType.Int) raceId = m[5].AsUInt32();
        }

        return true;
    }
}
