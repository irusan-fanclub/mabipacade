using System.Text;

namespace Mabipacade.DebugUi.Models;

/// <summary>
/// One 16-byte row of the hex pane. The bytes column is carried as three
/// segments so the view can paint the middle one: Pre + Hit + Post always
/// re-joins to <see cref="HexBytes"/>, and Hit is empty on rows outside the
/// highlighted range.
/// </summary>
public sealed record HexDumpLine(int Offset, string HexBytes, string Ascii,
    string HexPre = "", string HexHit = "", string HexPost = "")
{
    public bool HasHit => HexHit.Length > 0;

    public static IEnumerable<HexDumpLine> From(byte[] data)
        => From(data, highlightOffset: -1, highlightLength: 0);

    /// <summary>
    /// Same dump with the byte range [highlightOffset, +highlightLength)
    /// carried as the Hit segment of the rows it crosses.
    /// </summary>
    public static IEnumerable<HexDumpLine> From(byte[] data, int highlightOffset, int highlightLength)
    {
        const int width = 16;
        int hlEnd = highlightOffset + highlightLength;
        for (int i = 0; i < data.Length; i += width)
        {
            int len = Math.Min(width, data.Length - i);
            var ascii = new StringBuilder(width);
            for (int j = 0; j < len; j++)
            {
                byte b = data[i + j];
                ascii.Append(b is >= 0x20 and < 0x7F ? (char)b : '.');
            }

            // The highlight clipped to this row, in row-local byte indices.
            int hitFrom = Math.Clamp(highlightOffset - i, 0, len);
            int hitTo = Math.Clamp(hlEnd - i, 0, len);

            string hex = HexSegment(data, i, 0, len);
            string pre, hit, post;
            if (hitTo > hitFrom)
            {
                pre = HexSegment(data, i, 0, hitFrom);
                hit = HexSegment(data, i, hitFrom, hitTo);
                post = HexSegment(data, i, hitTo, len);
                // Joining spaces go on the un-highlighted segments, so the
                // painted background hugs the hit bytes exactly.
                if (pre.Length > 0) pre += " ";
                if (post.Length > 0) post = " " + post;
            }
            else
            {
                pre = hex;
                hit = "";
                post = "";
            }

            yield return new HexDumpLine(i, hex, ascii.ToString(), pre, hit, post);
        }
    }

    /// <summary>Bytes [from, to) of the row starting at <paramref name="rowStart"/>, space-separated.</summary>
    private static string HexSegment(byte[] data, int rowStart, int from, int to)
    {
        if (to <= from) return "";
        var sb = new StringBuilder((to - from) * 3);
        for (int j = from; j < to; j++)
        {
            if (j > from) sb.Append(' ');
            sb.Append(data[rowStart + j].ToString("X2"));
        }
        return sb.ToString();
    }
}
