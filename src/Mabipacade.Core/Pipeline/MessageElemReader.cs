using System.Buffers.Binary;
using System.Text;
using Mabipacade.Core.Model;

namespace Mabipacade.Core.Pipeline;

public enum ReadElemsResult { Ok, BadBody }

/// <summary>Byte range of one elem within the message body: tag byte through the end of its payload.</summary>
public readonly record struct ElemSpan(int Offset, int Length);

public static class MessageElemReader
{
    public static ReadElemsResult TryRead(ReadOnlySpan<byte> body, out IReadOnlyList<MessageElem> elems)
        => TryReadCore(body, out elems, null);

    /// <summary>
    /// Same read, but also reports where each elem sits in the body. The spans
    /// come from the walk itself, so they cannot drift from what was parsed.
    /// </summary>
    public static ReadElemsResult TryReadWithSpans(ReadOnlySpan<byte> body,
        out IReadOnlyList<MessageElem> elems, out IReadOnlyList<ElemSpan> spans)
    {
        var list = new List<ElemSpan>();
        var result = TryReadCore(body, out elems, list);
        spans = result == ReadElemsResult.Ok ? list : Array.Empty<ElemSpan>();
        return result;
    }

    private static ReadElemsResult TryReadCore(ReadOnlySpan<byte> body,
        out IReadOnlyList<MessageElem> elems, List<ElemSpan>? spans)
    {
        elems = Array.Empty<MessageElem>();

        // Skip the outer reserved uvarint.
        if (!Uvarint.TryRead(body, out _, out int consumed)) return ReadElemsResult.BadBody;
        int offset = consumed;

        // Read the elem count uvarint.
        if (!Uvarint.TryRead(body.Slice(offset), out ulong count, out consumed)) return ReadElemsResult.BadBody;
        offset += consumed;

        // Skip the reserved zero byte.
        if (offset >= body.Length) return ReadElemsResult.BadBody;
        offset += 1;

        if (count > (ulong)Array.MaxLength) return ReadElemsResult.BadBody;
        var list = new List<MessageElem>((int)count);
        for (ulong i = 0; i < count; i++)
        {
            if (offset >= body.Length) return ReadElemsResult.BadBody;
            int start = offset;
            byte tag = body[offset++];
            switch ((MessageElemType)tag)
            {
                case MessageElemType.Byte:
                    if (offset + 1 > body.Length) return ReadElemsResult.BadBody;
                    list.Add(MessageElem.Byte(body[offset]));
                    offset += 1;
                    break;
                case MessageElemType.Short:
                    if (offset + 2 > body.Length) return ReadElemsResult.BadBody;
                    list.Add(MessageElem.Short(BinaryPrimitives.ReadUInt16BigEndian(body.Slice(offset, 2))));
                    offset += 2;
                    break;
                case MessageElemType.Int:
                    if (offset + 4 > body.Length) return ReadElemsResult.BadBody;
                    list.Add(MessageElem.Int(BinaryPrimitives.ReadUInt32BigEndian(body.Slice(offset, 4))));
                    offset += 4;
                    break;
                case MessageElemType.Long:
                    if (offset + 8 > body.Length) return ReadElemsResult.BadBody;
                    list.Add(MessageElem.Long(BinaryPrimitives.ReadUInt64BigEndian(body.Slice(offset, 8))));
                    offset += 8;
                    break;
                case MessageElemType.Float:
                    if (offset + 4 > body.Length) return ReadElemsResult.BadBody;
                    list.Add(MessageElem.Float(BinaryPrimitives.ReadSingleLittleEndian(body.Slice(offset, 4))));
                    offset += 4;
                    break;
                case MessageElemType.String:
                {
                    if (offset + 2 > body.Length) return ReadElemsResult.BadBody;
                    ushort len = BinaryPrimitives.ReadUInt16BigEndian(body.Slice(offset, 2));
                    offset += 2;
                    if (offset + len > body.Length) return ReadElemsResult.BadBody;
                    int visible = len == 0 ? 0 : len - 1;        // strip trailing NUL
                    list.Add(MessageElem.String(Encoding.UTF8.GetString(body.Slice(offset, visible))));
                    offset += len;
                    break;
                }
                case MessageElemType.Bin:
                {
                    if (offset + 2 > body.Length) return ReadElemsResult.BadBody;
                    ushort len = BinaryPrimitives.ReadUInt16BigEndian(body.Slice(offset, 2));
                    offset += 2;
                    if (offset + len > body.Length) return ReadElemsResult.BadBody;
                    list.Add(MessageElem.Bin(body.Slice(offset, len).ToArray()));
                    offset += len;
                    break;
                }
                default:
                    return ReadElemsResult.BadBody;
            }
            spans?.Add(new ElemSpan(start, offset - start));
        }
        elems = list;
        return ReadElemsResult.Ok;
    }
}
