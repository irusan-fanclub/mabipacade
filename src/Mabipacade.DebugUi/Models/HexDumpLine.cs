using System.Text;

namespace Mabipacade.DebugUi.Models;

public sealed record HexDumpLine(int Offset, string HexBytes, string Ascii)
{
    public static IEnumerable<HexDumpLine> From(byte[] data)
    {
        const int width = 16;
        for (int i = 0; i < data.Length; i += width)
        {
            int len = Math.Min(width, data.Length - i);
            var hex = new StringBuilder(width * 3);
            var ascii = new StringBuilder(width);
            for (int j = 0; j < len; j++)
            {
                byte b = data[i + j];
                hex.Append(b.ToString("X2")).Append(' ');
                ascii.Append(b is >= 0x20 and < 0x7F ? (char)b : '.');
            }
            yield return new HexDumpLine(i, hex.ToString().TrimEnd(), ascii.ToString());
        }
    }
}
