using Mabipacade.Core.Model;

namespace Mabipacade.Decoders.Snapshot;

/// <summary>
/// Raised when a section reads an element of the wrong type or runs off the end.
/// The 0x5209 body carries no length prefixes: sections are handed a shared
/// cursor and each returns it advanced, so one bad read corrupts every section
/// after it. Failing loudly here lets the orchestrator record where alignment
/// was lost instead of emitting plausible garbage for the rest of the packet.
/// </summary>
public sealed class SnapshotFormatException : Exception
{
    public int Index { get; }

    public SnapshotFormatException(int index, string message) : base(message)
        => Index = index;
}

/// <summary>
/// Sequential reader over a message's element array. Every read advances the
/// cursor; nothing seeks backwards, mirroring how the client's
/// <c>CCharacter::ProcessDataMessage</c> passes one message cursor down the
/// component chain.
/// </summary>
internal sealed class ElemCursor
{
    private readonly IReadOnlyList<MessageElem> _e;

    public ElemCursor(IReadOnlyList<MessageElem> elems, int start = 0)
    {
        _e = elems;
        Index = start;
    }

    public int Index { get; private set; }
    public int Count => _e.Count;
    public int Remaining => _e.Count - Index;
    public bool AtEnd => Index >= _e.Count;

    public byte U8() => Take(MessageElemType.Byte).AsByte();
    public ushort U16() => Take(MessageElemType.Short).AsUInt16();
    public uint U32() => Take(MessageElemType.Int).AsUInt32();
    public ulong U64() => Take(MessageElemType.Long).AsUInt64();
    public float F32() => Take(MessageElemType.Float).AsFloat();
    public string Str() => Take(MessageElemType.String).AsString();
    public byte[] Bin() => Take(MessageElemType.Bin).AsBytes();

    /// <summary>
    /// The client's ReadBool (<c>0x14039AD70</c>) is <c>ReadU8() != 0</c>, so it
    /// consumes a Byte element like any other u8.
    /// </summary>
    public bool Bool() => U8() != 0;

    /// <summary>
    /// Signed 8-bit fields still arrive as Byte elements; the sign is the
    /// reader's interpretation, not a distinct wire type.
    /// </summary>
    public sbyte I8() => unchecked((sbyte)U8());

    public short I16() => unchecked((short)U16());

    public int I32() => unchecked((int)U32());

    /// <summary>
    /// Consumes one element of whatever type it is. For positional blocks whose
    /// slot meaning is fixed but whose wire type varies per slot.
    /// </summary>
    public MessageElem Any()
    {
        Require(1);
        return _e[Index++];
    }

    /// <summary>Advances without reading. Used for sections whose length is known but whose fields are not.</summary>
    public void Skip(int n)
    {
        if (n < 0) throw new SnapshotFormatException(Index, $"Cannot skip {n} elements");
        Require(n);
        Index += n;
    }

    public MessageElem Peek(int offset = 0)
    {
        int i = Index + offset;
        if (i < 0 || i >= _e.Count)
            throw new SnapshotFormatException(i, $"Peek past end (index {i}, count {_e.Count})");
        return _e[i];
    }

    /// <summary>Type at <paramref name="offset"/>, or null when past the end. Never throws.</summary>
    public MessageElemType? PeekType(int offset = 0)
    {
        int i = Index + offset;
        return i >= 0 && i < _e.Count ? _e[i].Type : null;
    }

    /// <summary>
    /// The raw elements of a span already consumed. Sections whose field
    /// meanings are not yet established expose their slice this way, so callers
    /// keep the data even when the parser only knows the length.
    /// </summary>
    public IReadOnlyList<MessageElem> Slice(int start, int count)
    {
        if (start < 0 || count < 0 || start + count > _e.Count)
            throw new SnapshotFormatException(start, $"Slice {start}+{count} out of range (count {_e.Count})");
        var slice = new MessageElem[count];
        for (int i = 0; i < count; i++) slice[i] = _e[start + i];
        return slice;
    }

    private MessageElem Take(MessageElemType expected)
    {
        Require(1);
        var el = _e[Index];
        if (el.Type != expected)
            throw new SnapshotFormatException(Index,
                $"Expected {expected} at element {Index}, found {el.Type}");
        Index++;
        return el;
    }

    private void Require(int n)
    {
        if (Remaining < n)
            throw new SnapshotFormatException(Index,
                $"Need {n} element(s) at {Index} but only {Remaining} remain");
    }
}
