using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Entity;

/// <summary>
/// One entity of a batch appear. <see cref="Name"/> and <see cref="RaceId"/>
/// are filled only for character entries (type 16, data-type byte 5), whose
/// nested body shares 0x520C's layout: { Long id, Byte 5, String name,
/// String, String, Int raceId, … }.
/// </summary>
public sealed record EntitiesAppearEntry(ushort EntryType, ulong EntityId, string? Name, uint? RaceId);

public sealed record EntitiesAppear(int DeclaredCount, IReadOnlyList<EntitiesAppearEntry> Entries);

/// <summary>
/// 0x5334 EntitiesAppear — sent when many entities enter view at once.
/// Body is { Short count } then per entry { Short type, Int length, Bin body }.
/// The Bin is a nested packet body (zeroed op + id, then a normal message)
/// re-parsed with the shared element reader. Entry types seen: 16 character,
/// 80 and 160 not yet identified. Layout per mogugi's
/// ParseEntitiesAppearPacket, verified against 23 captured packets.
/// </summary>
public sealed class EntitiesAppearDecoder : IPacketDecoder
{
    public uint Op => 0x00005334;

    public object Decode(DecoderInput input)
    {
        var e = input.Elems;
        int declared = e.Count > 0 && e[0].Type == MessageElemType.Short ? e[0].AsUInt16() : 0;

        var entries = new List<EntitiesAppearEntry>();
        for (int i = 1; i + 2 < e.Count; i += 3)
        {
            if (e[i].Type != MessageElemType.Short || e[i + 2].Type != MessageElemType.Bin)
                break;
            entries.Add(ReadEntry(e[i].AsUInt16(), e[i + 2].AsBytes()));
        }

        return new EntitiesAppear(declared, entries);
    }

    private static EntitiesAppearEntry ReadEntry(ushort entryType, byte[] body)
    {
        NestedAppearBody.TryRead(body, out ulong id, out string? name, out uint? raceId);
        return new EntitiesAppearEntry(entryType, id, name, raceId);
    }
}
