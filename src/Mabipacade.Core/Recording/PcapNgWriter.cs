using System.Buffers.Binary;
using System.Text;
using PacketDotNet;

namespace Mabipacade.Core.Recording;

/// <summary>
/// Writes a capture in pcapng. libpcap only writes classic pcap — SharpPcap
/// exposes no pcapng writer — so the block layout is assembled here.
///
/// The file is a Section Header Block, one Interface Description Block, then an
/// Enhanced Packet Block per frame. Every block is 4-byte aligned and repeats
/// its own total length at the end, which is what lets a reader walk the chain.
/// Little-endian throughout, as the section header's byte-order magic declares.
/// </summary>
public sealed class PcapNgWriter : IDisposable
{
    private const uint BlockTypeSectionHeader = 0x0A0D0D0A;
    private const uint BlockTypeInterfaceDescription = 1;
    private const uint BlockTypeEnhancedPacket = 6;
    private const uint ByteOrderMagic = 0x1A2B3C4D;

    /// <summary>Nothing here truncates the frame, so the snap length is the format's maximum.</summary>
    private const uint SnapLen = 0x00040000;

    /// <summary>
    /// if_tsresol = 9 declares nanosecond timestamps. A DateTime tick is 100ns,
    /// so every recorded timestamp is expressible exactly — unlike classic pcap,
    /// whose fixed microsecond field dropped the sub-microsecond part.
    /// </summary>
    private const byte TimestampResolutionNanos = 9;

    private const ushort OptionEndOfOptions = 0;
    private const ushort OptionComment = 1;
    private const ushort OptionIfName = 2;
    private const ushort OptionIfDescription = 3;
    private const ushort OptionIfTsResol = 9;
    private const ushort OptionIfFilter = 11;
    private const ushort OptionShbUserAppl = 4;

    private readonly Stream _stream;
    private readonly LinkLayers _linkLayer;
    private readonly object _gate = new();

    public PcapNgWriter(
        string path,
        LinkLayers linkLayer,
        string? interfaceName = null,
        string? interfaceDescription = null,
        string? captureFilter = null,
        string? application = null)
        : this(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read),
               linkLayer, interfaceName, interfaceDescription, captureFilter, application)
    {
    }

    public PcapNgWriter(
        Stream stream,
        LinkLayers linkLayer,
        string? interfaceName = null,
        string? interfaceDescription = null,
        string? captureFilter = null,
        string? application = null)
    {
        _stream = stream;
        _linkLayer = linkLayer;
        WriteSectionHeader(application);
        WriteInterfaceDescription(interfaceName, interfaceDescription, captureFilter);
    }

    /// <summary>
    /// Appends one frame. Safe to call from a capture callback: writes are
    /// serialised, but the caller is responsible for keeping disk I/O off the
    /// capture thread when throughput matters.
    /// </summary>
    public void Write(byte[] frame, DateTime timestampUtc)
    {
        // Nanoseconds since the Unix epoch, split high/low as the format wants.
        ulong nanos = (ulong)(timestampUtc - DateTime.UnixEpoch).Ticks * 100UL;

        var body = new byte[20 + Pad4(frame.Length)];
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(0), 0);                    // interface id
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(4), (uint)(nanos >> 32));
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(8), (uint)nanos);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(12), (uint)frame.Length);  // captured
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(16), (uint)frame.Length);  // original
        frame.CopyTo(body.AsSpan(20));

        lock (_gate) WriteBlock(BlockTypeEnhancedPacket, body);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _stream.Flush();
            _stream.Dispose();
        }
    }

    private void WriteSectionHeader(string? application)
    {
        var options = new MemoryStream();
        if (!string.IsNullOrEmpty(application))
            WriteOption(options, OptionShbUserAppl, Encoding.UTF8.GetBytes(application));
        EndOptions(options);

        var body = new byte[16 + options.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(0), ByteOrderMagic);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(4), 1);   // major version
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(6), 0);   // minor version
        // Section length unspecified: the size is not known while still recording.
        BinaryPrimitives.WriteUInt64LittleEndian(body.AsSpan(8), ulong.MaxValue);
        options.ToArray().CopyTo(body.AsSpan(16));

        WriteBlock(BlockTypeSectionHeader, body);
    }

    private void WriteInterfaceDescription(string? name, string? description, string? filter)
    {
        var options = new MemoryStream();
        if (!string.IsNullOrEmpty(name))
            WriteOption(options, OptionIfName, Encoding.UTF8.GetBytes(name));
        if (!string.IsNullOrEmpty(description))
            WriteOption(options, OptionIfDescription, Encoding.UTF8.GetBytes(description));
        if (!string.IsNullOrEmpty(filter))
            // A filter option is prefixed by a byte saying how it is expressed;
            // 0 means a libpcap filter string.
            WriteOption(options, OptionIfFilter,
                new byte[] { 0 }.Concat(Encoding.UTF8.GetBytes(filter)).ToArray());
        WriteOption(options, OptionIfTsResol, new[] { TimestampResolutionNanos });
        EndOptions(options);

        var body = new byte[8 + options.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(0), (ushort)_linkLayer);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(2), 0);   // reserved
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(4), SnapLen);
        options.ToArray().CopyTo(body.AsSpan(8));

        WriteBlock(BlockTypeInterfaceDescription, body);
    }

    /// <summary>Frames a body as a block: type, total length, body, total length again.</summary>
    private void WriteBlock(uint type, byte[] body)
    {
        uint total = (uint)(12 + body.Length);
        Span<byte> header = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(header, type);
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], total);
        _stream.Write(header);
        _stream.Write(body);
        Span<byte> trailer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(trailer, total);
        _stream.Write(trailer);
    }

    private static void WriteOption(Stream s, ushort code, byte[] value)
    {
        Span<byte> head = stackalloc byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(head, code);
        BinaryPrimitives.WriteUInt16LittleEndian(head[2..], (ushort)value.Length);
        s.Write(head);
        s.Write(value);
        for (int i = value.Length; i < Pad4(value.Length); i++) s.WriteByte(0);
    }

    private static void EndOptions(Stream s) => WriteOption(s, OptionEndOfOptions, Array.Empty<byte>());

    private static int Pad4(int n) => (n + 3) & ~3;
}
