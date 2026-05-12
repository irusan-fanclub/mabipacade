using PacketDotNet;

namespace Mabipacade.Core.Model;

internal sealed record RawFrame(
    byte[] Data,
    LinkLayers LinkLayer,
    DateTime TimestampUtc);
