using System.IO.Compression;
using System.Text.Json;
using Mabipacade.Core.Model;

namespace Mabipacade.Decoders.Tests.Snapshot;

/// <summary>
/// Loads the recorded TW G28S1 0x5209 packet from the gzipped NDJSON fixture.
/// The reference analysis (mabi_it_workspace research/packet-0x5209) is aligned
/// against this exact capture, so its element indices are quotable as expected
/// values.
/// </summary>
internal static class SnapshotFixture
{
    private const string RelativePath = "fixtures/0x5209-tw-g28s1.jsonl.gz";

    private static readonly Lazy<IReadOnlyList<MessageElem>> _elems = new(Load);

    /// <summary>The capture's 23,976 elements, including the two header ones.</summary>
    public static IReadOnlyList<MessageElem> Elems => _elems.Value;

    private static IReadOnlyList<MessageElem> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, RelativePath);
        if (!File.Exists(path))
            throw new FileNotFoundException($"0x5209 fixture missing: {path}");

        using var file = File.OpenRead(path);
        using var gz = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gz);

        var line = reader.ReadLine()
            ?? throw new InvalidDataException("0x5209 fixture is empty");

        using var doc = JsonDocument.Parse(line);
        var elems = doc.RootElement.GetProperty("elems");

        var list = new List<MessageElem>(elems.GetArrayLength());
        foreach (var el in elems.EnumerateArray())
            list.Add(Parse(el));
        return list;
    }

    private static MessageElem Parse(JsonElement el)
    {
        var t = el.GetProperty("t").GetString();
        var v = el.GetProperty("v");
        return t switch
        {
            // Numeric values above 2^53 are written as JSON strings by
            // NdjsonWriter, so every integer type accepts both forms.
            "Byte" => MessageElem.Byte(byte.Parse(Raw(v))),
            "Short" => MessageElem.Short(ushort.Parse(Raw(v))),
            "Int" => MessageElem.Int(uint.Parse(Raw(v))),
            "Long" => MessageElem.Long(ulong.Parse(Raw(v))),
            "Float" => MessageElem.Float(float.Parse(Raw(v))),
            "String" => MessageElem.String(v.GetString() ?? ""),
            "Bin" => MessageElem.Bin(ParseBinary(v.GetString() ?? "")),
            _ => throw new InvalidDataException($"Unknown elem type '{t}'"),
        };
    }

    /// <summary>
    /// Binary elements arrive hex-encoded in this capture but base64-encoded
    /// from <c>ElemJson</c>, so both are accepted. Getting this wrong is quiet
    /// and damaging: base64-decoding hex yields exactly 1.5× the bytes, all of
    /// them wrong, while every element count still lines up.
    /// </summary>
    private static byte[] ParseBinary(string s)
        => IsHex(s) ? Convert.FromHexString(s) : Convert.FromBase64String(s);

    private static bool IsHex(string s)
    {
        if (s.Length == 0 || s.Length % 2 != 0) return false;
        foreach (char ch in s)
            if (!char.IsAsciiHexDigit(ch)) return false;
        return true;
    }

    private static string Raw(JsonElement v) =>
        v.ValueKind == JsonValueKind.String ? v.GetString()! : v.GetRawText();
}
