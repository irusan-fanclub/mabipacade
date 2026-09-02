using Mabipacade.Core.Crypto;

namespace Mabipacade.Core.Tests.Crypto;

public class MabiClientCipherTests
{
    private static byte[] Hex(string s) => Convert.FromHexString(s);

    // Golden vectors captured from the reference cipher (seed 0x12345678),
    // then reproduced bit-for-bit by this implementation. The packet is
    // [sign][len LE:4][flag][op BE:4][eid BE:8][elem prefix] with no checksum,
    // so four zero bytes stand in for the trailing checksum the cipher excludes.
    private const uint GoldenSeed = 0x12345678;
    private const string Plain      = "881500000000000069840000000000000001000000";
    private const string Cipher1    = "881500000000276775D888939BB1FDF7461ED0E1DD";
    private const string Cipher2    = "8815000000008EFB10FF27671CDC88939BB0FDF746";

    private static byte[] WithChecksumRoom(string hex) =>
        Hex(hex).Concat(new byte[] { 0, 0, 0, 0 }).ToArray();

    [Fact]
    public void DecryptPacket_MatchesReferenceCipher_AndAdvancesPerPacket()
    {
        var cipher = new MabiClientCipher(GoldenSeed);

        var p1 = WithChecksumRoom(Plain);
        cipher.DecryptPacket(p1);
        Assert.Equal(Cipher1, Convert.ToHexString(p1.AsSpan(0, 21).ToArray()));

        // A second packet is transformed with the advanced state, not repeated.
        var p2 = WithChecksumRoom(Plain);
        cipher.DecryptPacket(p2);
        Assert.Equal(Cipher2, Convert.ToHexString(p2.AsSpan(0, 21).ToArray()));
    }

    [Fact]
    public void DecryptPacket_IsItsOwnInverse()
    {
        // XOR is symmetric: encrypting then decrypting with a fresh cipher at
        // the same state returns the original bytes.
        var encoder = new MabiClientCipher(GoldenSeed);
        var decoder = new MabiClientCipher(GoldenSeed);

        var buf = WithChecksumRoom(Plain);
        encoder.DecryptPacket(buf);
        Assert.NotEqual(Plain, Convert.ToHexString(buf.AsSpan(0, 21).ToArray()));

        decoder.DecryptPacket(buf);
        Assert.Equal(Plain, Convert.ToHexString(buf.AsSpan(0, 21).ToArray()));
    }

    [Fact]
    public void DecryptPacket_LeavesHeaderAndChecksumUntouched()
    {
        var cipher = new MabiClientCipher(GoldenSeed);
        var buf = WithChecksumRoom(Plain);
        var before = (byte[])buf.Clone();

        cipher.DecryptPacket(buf);

        // Header [0,6) unchanged; the 4 checksum bytes [len-4,len) unchanged.
        Assert.Equal(before.AsSpan(0, 6).ToArray(), buf.AsSpan(0, 6).ToArray());
        Assert.Equal(before.AsSpan(buf.Length - 4).ToArray(), buf.AsSpan(buf.Length - 4).ToArray());
    }
}
