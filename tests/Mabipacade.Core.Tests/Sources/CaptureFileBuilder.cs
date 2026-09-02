using System.Buffers.Binary;

namespace Mabipacade.Core.Tests.Sources;

/// <summary>
/// Builds pcap and pcapng files byte by byte so capture-reading tests need no
/// external fixture and can express malformed files exactly. Little-endian
/// throughout, which is what both formats' magic numbers below declare.
/// </summary>
internal static class CaptureFileBuilder
{
    private const uint LinkTypeEthernet = 1;
    private const uint SnapLen = 65535;

    // --- pcapng ----------------------------------------------------------

    /// <summary>
    /// Delegates to the production writer so the format has one implementation.
    /// The malformed variants below start from its output and damage it, which
    /// is what makes them faithful to how real files break.
    /// </summary>
    public static byte[] PcapNg(params byte[][] frames)
    {
        var ms = new MemoryStream();
        using (var w = new Mabipacade.Core.Recording.PcapNgWriter(
                   ms, PacketDotNet.LinkLayers.Ethernet))
        {
            foreach (var f in frames) w.Write(f, DateTime.UnixEpoch);
        }
        return ms.ToArray();
    }

    /// <summary>Total length of the block starting at <paramref name="offset"/>.</summary>
    private static uint BlockLengthAt(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4));

    /// <summary>Offset just past the section header and interface description blocks.</summary>
    private static int HeaderLength(byte[] whole)
    {
        int offset = (int)BlockLengthAt(whole, 0);
        return offset + (int)BlockLengthAt(whole, offset);
    }

    /// <summary>
    /// A pcapng whose last packet block is cut short, as happens when the
    /// recording process is killed before its buffered write completes.
    /// </summary>
    public static byte[] PcapNgWithTruncatedTail(params byte[][] frames)
    {
        var whole = PcapNg(frames);
        int lastBlockStart = whole.Length - (int)BlockLengthAt(whole, LastBlockStart(whole));
        // Keep the block's type and length fields but only part of its body,
        // so the reader commits to a block it cannot finish.
        return whole[..(lastBlockStart + 16)];
    }

    /// <summary>
    /// A structurally valid header followed by an unreadable first packet —
    /// the file opens, then yields nothing.
    /// </summary>
    public static byte[] PcapNgWithNoReadableFrame(byte[] frame)
    {
        var whole = PcapNg(frame);
        return whole[..(HeaderLength(whole) + 16)];
    }

    /// <summary>Offset of the final block, found by walking the chain.</summary>
    private static int LastBlockStart(byte[] whole)
    {
        int offset = 0, previous = 0;
        while (offset < whole.Length)
        {
            previous = offset;
            offset += (int)BlockLengthAt(whole, offset);
        }
        return previous;
    }

    private static int Pad4(int n) => (n + 3) & ~3;

    // --- classic pcap ----------------------------------------------------

    public static byte[] Pcap(params byte[][] frames)
    {
        var ms = new MemoryStream();

        Span<byte> hdr = stackalloc byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(hdr[0..], 0xA1B2C3D4); // magic
        BinaryPrimitives.WriteUInt16LittleEndian(hdr[4..], 2);          // version major
        BinaryPrimitives.WriteUInt16LittleEndian(hdr[6..], 4);          // version minor
        BinaryPrimitives.WriteInt32LittleEndian(hdr[8..], 0);           // thiszone
        BinaryPrimitives.WriteUInt32LittleEndian(hdr[12..], 0);         // sigfigs
        BinaryPrimitives.WriteUInt32LittleEndian(hdr[16..], SnapLen);
        BinaryPrimitives.WriteUInt32LittleEndian(hdr[20..], LinkTypeEthernet);
        ms.Write(hdr);

        Span<byte> rec = stackalloc byte[16];
        foreach (var f in frames)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(rec[0..], 1_600_000_000); // ts sec
            BinaryPrimitives.WriteUInt32LittleEndian(rec[4..], 0);             // ts usec
            BinaryPrimitives.WriteUInt32LittleEndian(rec[8..], (uint)f.Length); // captured
            BinaryPrimitives.WriteUInt32LittleEndian(rec[12..], (uint)f.Length); // original
            ms.Write(rec);
            ms.Write(f);
        }

        return ms.ToArray();
    }

    // --- scratch files ---------------------------------------------------

    /// <summary>Writes bytes to a temp file that deletes itself on dispose.</summary>
    public static TempCaptureFile WriteTemp(byte[] bytes, string extension)
    {
        var path = Path.Combine(Path.GetTempPath(),
            $"mabipacade-test-{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(path, bytes);
        return new TempCaptureFile(path);
    }

    internal sealed class TempCaptureFile : IDisposable
    {
        public string Path { get; }
        public TempCaptureFile(string path) { Path = path; }
        public void Dispose()
        {
            try { File.Delete(Path); } catch (IOException) { }
        }
    }
}
