using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Ui;

/// <summary>
/// Shared base for the 0x65A2-A6 / 0x6652-6653 "URL update" family: each carries
/// N String elems (URL templates with $(VAR) placeholders). Distinct record types
/// per opcode keep them identifiable downstream.
/// </summary>
public abstract record UrlUpdate(IReadOnlyList<string> Urls);

/// <summary>0x65A2 UrlUpdateChronicle — chronicle web URL templates (3 strings).</summary>
public sealed record UrlUpdateChronicle(IReadOnlyList<string> Urls) : UrlUpdate(Urls);

/// <summary>0x65A3 UrlUpdateAdvertise — advertise web URL templates.</summary>
public sealed record UrlUpdateAdvertise(IReadOnlyList<string> Urls) : UrlUpdate(Urls);

/// <summary>0x65A4 UrlUpdateGuestbook — guestbook web URL templates.</summary>
public sealed record UrlUpdateGuestbook(IReadOnlyList<string> Urls) : UrlUpdate(Urls);

/// <summary>0x65A5 UrlUpdatePvp — pvp web URL templates.</summary>
public sealed record UrlUpdatePvp(IReadOnlyList<string> Urls) : UrlUpdate(Urls);

/// <summary>0x65A6 UrlUpdateDungeonBoard — dungeon board web URL templates.</summary>
public sealed record UrlUpdateDungeonBoard(IReadOnlyList<string> Urls) : UrlUpdate(Urls);

/// <summary>0x6652 UrlUpdateReserved1 — TW reserved URL slot (2 empty strings observed).</summary>
public sealed record UrlUpdateReserved1(IReadOnlyList<string> Urls) : UrlUpdate(Urls);

/// <summary>0x6653 UrlUpdateReserved2 — TW reserved URL slot (2 empty strings observed).</summary>
public sealed record UrlUpdateReserved2(IReadOnlyList<string> Urls) : UrlUpdate(Urls);

internal static class UrlUpdateHelper
{
    public static List<string> CollectStrings(DecoderInput input)
    {
        var urls = new List<string>();
        foreach (var el in input.Elems)
            if (el.Type == MessageElemType.String)
                urls.Add(el.AsString());
        return urls;
    }
}

public sealed class UrlUpdateChronicleDecoder : IPacketDecoder
{
    public uint Op => 0x000065A2;
    public object Decode(DecoderInput input) => new UrlUpdateChronicle(UrlUpdateHelper.CollectStrings(input));
}

public sealed class UrlUpdateAdvertiseDecoder : IPacketDecoder
{
    public uint Op => 0x000065A3;
    public object Decode(DecoderInput input) => new UrlUpdateAdvertise(UrlUpdateHelper.CollectStrings(input));
}

public sealed class UrlUpdateGuestbookDecoder : IPacketDecoder
{
    public uint Op => 0x000065A4;
    public object Decode(DecoderInput input) => new UrlUpdateGuestbook(UrlUpdateHelper.CollectStrings(input));
}

public sealed class UrlUpdatePvpDecoder : IPacketDecoder
{
    public uint Op => 0x000065A5;
    public object Decode(DecoderInput input) => new UrlUpdatePvp(UrlUpdateHelper.CollectStrings(input));
}

public sealed class UrlUpdateDungeonBoardDecoder : IPacketDecoder
{
    public uint Op => 0x000065A6;
    public object Decode(DecoderInput input) => new UrlUpdateDungeonBoard(UrlUpdateHelper.CollectStrings(input));
}

public sealed class UrlUpdateReserved1Decoder : IPacketDecoder
{
    public uint Op => 0x00006652;
    public object Decode(DecoderInput input) => new UrlUpdateReserved1(UrlUpdateHelper.CollectStrings(input));
}

public sealed class UrlUpdateReserved2Decoder : IPacketDecoder
{
    public uint Op => 0x00006653;
    public object Decode(DecoderInput input) => new UrlUpdateReserved2(UrlUpdateHelper.CollectStrings(input));
}
