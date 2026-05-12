namespace Mabipacade.Core.Pipeline;

internal static class Uvarint
{
    public static bool TryRead(ReadOnlySpan<byte> data, out ulong value, out int consumed)
    {
        value = 0;
        consumed = 0;
        int shift = 0;
        for (int i = 0; i < data.Length && i < 10; i++)
        {
            byte b = data[i];
            value |= ((ulong)(b & 0x7F)) << shift;
            consumed = i + 1;
            if ((b & 0x80) == 0) return true;
            shift += 7;
        }
        consumed = 0;
        value = 0;
        return false;
    }
}
