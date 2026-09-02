using System.Buffers.Binary;

namespace Mabipacade.Core.Crypto;

/// <summary>
/// Mabinogi's client→server stream cipher. The transform is its own inverse
/// (a keyed XOR), so the same call both encrypts and decrypts; here it is only
/// ever used to decrypt captured client traffic.
///
/// The cipher is stateful and order-dependent: after each packet it rotates
/// four bytes of its working key from the keystream and steps an index. The
/// state's evolution depends only on the seed and the number of packets
/// processed — not on packet contents — but a packet can still only be
/// decrypted once its predecessors on the connection have been, in order.
/// A packet whose header flag is <see cref="UnencryptedFlag"/> is sent in the
/// clear and must be skipped entirely (neither decrypted nor counted).
/// </summary>
public sealed class MabiClientCipher
{
    /// <summary>Header flag byte marking a packet the client left unencrypted.</summary>
    public const byte UnencryptedFlag = 0x03;

    /// <summary>Bytes of framing before the encrypted region: sign + length(4) + flag.</summary>
    private const int HeaderSize = 6;

    /// <summary>Trailing checksum the client appends; excluded from the cipher and the body.</summary>
    private const int ChecksumSize = 4;

    private readonly MabiKeystream _keystream;
    private readonly byte[] _key = new byte[128];
    private int _index;

    public MabiClientCipher(uint seed)
    {
        _keystream = new MabiKeystream(seed);
        for (var i = 31; i >= 0; i--)
            BinaryPrimitives.WriteUInt32LittleEndian(_key.AsSpan(i * 4), _keystream.Next());
        _index = 31;
    }

    /// <summary>
    /// Decrypts one whole on-wire client packet in place — <paramref name="packet"/>
    /// spans sign through the trailing checksum. The body [6, len-4) is
    /// transformed; the header, and the checksum, are left untouched. Advances
    /// the cipher state by one packet.
    /// </summary>
    public void DecryptPacket(Span<byte> packet)
    {
        // The bulk is XORed in 4-byte words against the first 32 bytes of the
        // key, cycled; the final sub-word tail uses the key at large.
        int end = packet.Length - ChecksumSize;
        int wordEnd = end - 4;   // last full-word boundary before the tail
        int i = HeaderSize;
        int t = _index * 4;

        for (; i < wordEnd; i += 4)
        {
            int j = t & 0x1F;
            t += 4;
            packet[i]     ^= _key[j];
            packet[i + 1] ^= _key[j + 1];
            packet[i + 2] ^= _key[j + 2];
            packet[i + 3] ^= _key[j + 3];
        }

        for (; i < end; i++, t++)
            packet[i] ^= _key[t & 0x7F];

        // Roll four bytes of the key forward, then step the index. _index stays
        // in [0, 32), so this only ever touches the cycled portion of the key.
        BinaryPrimitives.WriteUInt32LittleEndian(_key.AsSpan(_index), _keystream.Next());
        _index = _index == 0 ? 31 : _index - 1;
    }
}
