using System.Buffers.Binary;
using Mabipacade.Core.Capture;
using Mabipacade.Core.Crypto;
using Mabipacade.Core.Model;
using Mabipacade.Core.Pipeline;

namespace Mabipacade.Core.Tests.Pipeline;

public class OutboundDecryptionTests
{
    private const ushort ServerPort = 11022;   // inside the TW port range
    private const ushort ClientPort = 50000;
    private const uint Seed = 0x11223344;

    /// <summary>
    /// Builds one on-wire client→server packet: header, op, entity id, elem
    /// body, and a 4-byte checksum, with the body encrypted by the client
    /// cipher at <paramref name="cipher"/>'s current state (which it advances).
    /// </summary>
    private static byte[] EncryptedOutbound(MabiClientCipher cipher, uint op, ulong entityId, byte[] elemBody)
    {
        int length = 6 + 4 + 8 + elemBody.Length + 4;   // header + op + eid + body + checksum
        var packet = new byte[length];
        packet[0] = 0x88;                                             // sign
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(1, 4), (uint)length);
        packet[5] = 0x00;                                            // flag: encrypted
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(6, 4), op);
        BinaryPrimitives.WriteUInt64BigEndian(packet.AsSpan(10, 8), entityId);
        elemBody.CopyTo(packet.AsSpan(18));
        // XOR is symmetric, so decrypting a plaintext produces the ciphertext.
        cipher.DecryptPacket(packet);
        return packet;
    }

    // Elem stream: [outer uvarint=0][count][reserved 0] then elems.
    private static byte[] OneShortBody(ushort value) =>
        new byte[] { 0x00, 0x01, 0x00, 0x02, (byte)(value >> 8), (byte)value };

    private static async Task<List<MabiPacket>> RunAsync(params byte[][] frames)
    {
        var source = new SequenceFakeFrameSource(frames);
        var pipeline = new PacketPipeline(source, new DecoderRegistry(),
            DirectionClassifiers.ByServerEndpoint(RegionProfiles.Taiwan), decryptOutbound: true);
        var packets = new List<MabiPacket>();
        pipeline.PacketReceived += (_, p) => packets.Add(p);
        await pipeline.StartAsync(CancellationToken.None);
        await pipeline.StopAsync();
        return packets;
    }

    private const string ServerIp = "10.0.0.1";
    private const string ClientIp = "10.0.0.2";

    private static byte[] SeedFrame(uint seed)
    {
        var b = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, seed);
        // Server -> client: the 4-byte connection key that opens the stream.
        return TestEthernetBuilder.WrapTcp(b, srcPort: ServerPort, dstPort: ClientPort,
            srcIp: ServerIp, dstIp: ClientIp);
    }

    // Client -> server: the same connection mirrored, so its key folds onto the
    // seed frame's — otherwise the cipher would not be found.
    private static byte[] OutFrame(byte[] packet, uint seq) =>
        TestEthernetBuilder.WrapTcp(packet, srcPort: ClientPort, dstPort: ServerPort,
            srcIp: ClientIp, dstIp: ServerIp, sequenceNumber: seq);

    [Fact]
    public async Task DecryptsOutbound_UsingTheSeedFromTheConnectionKey()
    {
        var enc = new MabiClientCipher(Seed);
        var p1 = EncryptedOutbound(enc, 0x00001234, 0x0010_0000_0000_0001UL, OneShortBody(59000));
        var p2 = EncryptedOutbound(enc, 0x00005678, 0x0010_0000_0000_0002UL, OneShortBody(42));

        var packets = await RunAsync(
            SeedFrame(Seed),
            OutFrame(p1, 1000),
            OutFrame(p2, (uint)(1000 + p1.Length)));

        Assert.Equal(2, packets.Count);

        Assert.Equal(Direction.Outbound, packets[0].Direction);
        Assert.Equal(0x00001234u, packets[0].Op);
        Assert.Equal(0x0010_0000_0000_0001UL, packets[0].EntityId);
        Assert.Equal((ushort)59000, Assert.Single(packets[0].Elems).AsUInt16());

        Assert.Equal(0x00005678u, packets[1].Op);
        Assert.Equal((ushort)42, Assert.Single(packets[1].Elems).AsUInt16());
    }

    [Fact]
    public async Task WithoutTheSeed_OutboundStaysUndecrypted()
    {
        // No seed frame first: the connection was joined mid-stream, so the
        // cipher is never built and the encrypted body fails to parse.
        var enc = new MabiClientCipher(Seed);
        var p1 = EncryptedOutbound(enc, 0x00001234, 0x0010_0000_0000_0001UL, OneShortBody(59000));

        var packets = await RunAsync(OutFrame(p1, 1000));

        // The garbage body does not decode into the original packet.
        Assert.DoesNotContain(packets, p => p.Op == 0x00001234u);
    }

    [Fact]
    public async Task DisabledByDefault_OutboundIsNotDecrypted()
    {
        var enc = new MabiClientCipher(Seed);
        var p1 = EncryptedOutbound(enc, 0x00001234, 0x0010_0000_0000_0001UL, OneShortBody(59000));

        // decryptOutbound defaults off — even with the seed present, nothing decrypts.
        var source = new SequenceFakeFrameSource(SeedFrame(Seed), OutFrame(p1, 1000));
        var pipeline = new PacketPipeline(source, new DecoderRegistry(),
            DirectionClassifiers.ByServerEndpoint(RegionProfiles.Taiwan));
        var packets = new List<MabiPacket>();
        pipeline.PacketReceived += (_, p) => packets.Add(p);
        await pipeline.StartAsync(CancellationToken.None);
        await pipeline.StopAsync();

        Assert.DoesNotContain(packets, p => p.Op == 0x00001234u);
    }
}
