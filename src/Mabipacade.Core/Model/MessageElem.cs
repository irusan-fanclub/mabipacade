namespace Mabipacade.Core.Model;

public readonly struct MessageElem
{
    public MessageElemType Type { get; }
    private readonly object _value;

    private MessageElem(MessageElemType type, object value) { Type = type; _value = value; }

    public static MessageElem Byte(byte v)       => new(MessageElemType.Byte, v);
    public static MessageElem Short(ushort v)    => new(MessageElemType.Short, v);
    public static MessageElem Int(uint v)        => new(MessageElemType.Int, v);
    public static MessageElem Long(ulong v)      => new(MessageElemType.Long, v);
    public static MessageElem Float(float v)     => new(MessageElemType.Float, v);
    public static MessageElem String(string v)   => new(MessageElemType.String, v);
    public static MessageElem Bin(byte[] v)      => new(MessageElemType.Bin, v);

    public byte AsByte()       => Type == MessageElemType.Byte   ? (byte)_value   : throw Mismatch(MessageElemType.Byte);
    public ushort AsUInt16()   => Type == MessageElemType.Short  ? (ushort)_value : throw Mismatch(MessageElemType.Short);
    public uint AsUInt32()     => Type == MessageElemType.Int    ? (uint)_value   : throw Mismatch(MessageElemType.Int);
    public ulong AsUInt64()    => Type == MessageElemType.Long   ? (ulong)_value  : throw Mismatch(MessageElemType.Long);
    public float AsFloat()     => Type == MessageElemType.Float  ? (float)_value  : throw Mismatch(MessageElemType.Float);
    public string AsString()   => Type == MessageElemType.String ? (string)_value : throw Mismatch(MessageElemType.String);
    public byte[] AsBytes()    => Type == MessageElemType.Bin    ? (byte[])_value : throw Mismatch(MessageElemType.Bin);

    private InvalidOperationException Mismatch(MessageElemType expected) =>
        new($"Elem is {Type}, not {expected}");
}
