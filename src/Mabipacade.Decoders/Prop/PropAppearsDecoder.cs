using System.Buffers.Binary;
using Mabipacade.Core.Model;
using Mabipacade.Core.Plugins;

namespace Mabipacade.Decoders.Prop;

/// <summary>
/// 0x52D0 PropAppears — scene prop (door / light / pet-farm summon) broadcast.
/// First elem is a Bin (PropInfo struct, 60-68B). We capture the raw Bin and
/// parse PropId from its first 4 bytes (UInt32 LE).
/// </summary>
public sealed record PropAppears(uint PropId, IReadOnlyList<byte> PropInfo);

public sealed class PropAppearsDecoder : IPacketDecoder
{
    public uint Op => 0x000052D0;

    public object Decode(DecoderInput input)
    {
        foreach (var el in input.Elems)
        {
            if (el.Type != MessageElemType.Bin)
                continue;

            var bytes = el.AsBytes();
            var propId = bytes.Length >= 4
                ? BinaryPrimitives.ReadUInt32LittleEndian(bytes)
                : 0u;
            return new PropAppears(propId, bytes);
        }

        return new PropAppears(0u, Array.Empty<byte>());
    }
}
