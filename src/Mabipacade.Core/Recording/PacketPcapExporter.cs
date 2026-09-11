using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;
using PacketDotNet;
using Mabipacade.Core.Model;

namespace Mabipacade.Core.Recording;

/// <summary>
/// Writes decoded packets back out as a pcapng, one synthesized frame each.
///
/// The original capture frames are gone by the time a packet reaches the UI —
/// reassembly merges and splits them — so each frame here is synthesized: the
/// Mabi wire framing is rebuilt around the retained body (the exact inverse of
/// <see cref="Pipeline.MabiPacketFramer"/>) and wrapped in a fresh
/// Ethernet/IPv4/TCP envelope with placeholder addresses. The result opens in
/// Wireshark and, more importantly, replays through this app's own pipeline as
/// exactly the packets that were exported.
/// </summary>
public static class PacketPcapExporter
{
    private const string Application = "mabipacade packet export";

    // Placeholder endpoints, documented rather than hidden: the packet model
    // does not retain the real ones. 11020 is a typical Mabinogi world port.
    private static readonly IPAddress ServerIp = IPAddress.Parse("10.0.0.1");
    private static readonly IPAddress ClientIp = IPAddress.Parse("10.0.0.2");
    private const ushort ServerPort = 11020;
    private const ushort ClientPort = 52000;

    /// <summary>False for packets constructed without their raw body — there is nothing to re-frame.</summary>
    public static bool CanExport(MabiPacket packet) => packet.Body is { Length: > 0 };

    public static void Export(MabiPacket packet, string path) => Export(new[] { packet }, path);

    /// <summary>
    /// Writes the packets in the given order. Sequence numbers advance by each
    /// frame's payload per direction, so the flows read as one contiguous TCP
    /// stream — which is what lets the replay reassembler recover every packet
    /// instead of treating them as overlapping retransmits.
    /// </summary>
    public static void Export(IEnumerable<MabiPacket> packets, string path)
    {
        using var writer = new PcapNgWriter(path, LinkLayers.Ethernet, application: Application);
        uint seqIn = 1000, seqOut = 1000;
        foreach (var packet in packets)
        {
            bool inbound = packet.Direction == Direction.Inbound;
            var frame = BuildFrame(packet, inbound ? seqIn : seqOut);
            uint wireLength = (uint)(18 + packet.Body!.Length);
            if (inbound) seqIn += wireLength; else seqOut += wireLength;
            writer.Write(frame, packet.TimestampUtc);
        }
    }

    /// <summary>The synthesized Ethernet frame carrying the re-framed packet.</summary>
    public static byte[] BuildFrame(MabiPacket packet, uint sequenceNumber = 1000)
    {
        if (packet.Body is not { } body)
            throw new InvalidOperationException("packet has no raw body to export");

        // Layout mirrors MabiPacketFramer: [sign:1][length:4 LE][flag:1][op:4 BE][entityId:8 BE][body].
        int total = 6 + 4 + 8 + body.Length;
        var wire = new byte[total];
        wire[0] = 0x00;                                                       // sign
        BinaryPrimitives.WriteUInt32LittleEndian(wire.AsSpan(1, 4), (uint)total);
        wire[5] = 0x00;                                                       // flag = normal
        BinaryPrimitives.WriteUInt32BigEndian(wire.AsSpan(6, 4), packet.Op);
        BinaryPrimitives.WriteUInt64BigEndian(wire.AsSpan(10, 8), packet.EntityId);
        body.CopyTo(wire.AsSpan(18));

        // Inbound means server → client; keep the synthesized flow saying so.
        bool inbound = packet.Direction == Direction.Inbound;
        var tcp = new TcpPacket(inbound ? ServerPort : ClientPort,
                                inbound ? ClientPort : ServerPort)
        {
            PayloadData = wire,
            SequenceNumber = sequenceNumber,
            AcknowledgmentNumber = 1,
            Acknowledgment = true,
            Push = true,
            WindowSize = 65535,
        };
        var ip = new IPv4Packet(inbound ? ServerIp : ClientIp,
                                inbound ? ClientIp : ServerIp)
        {
            PayloadPacket = tcp,
            Protocol = ProtocolType.Tcp,
            TimeToLive = 64,
        };
        var eth = new EthernetPacket(
            new PhysicalAddress(new byte[] { 0x02, 0x00, 0x00, 0x00, 0x00, 0x01 }),
            new PhysicalAddress(new byte[] { 0x02, 0x00, 0x00, 0x00, 0x00, 0x02 }),
            EthernetType.IPv4)
        {
            PayloadPacket = ip,
        };

        // Valid checksums so Wireshark shows a clean frame instead of warnings.
        ip.UpdateIPChecksum();
        tcp.UpdateTcpChecksum();
        return eth.Bytes;
    }
}
