using Mabipacade.Core.Model;

namespace Mabipacade.Core.Plugins;

public readonly record struct DecoderInput(
    DateTime TimestampUtc,
    Direction Direction,
    uint Op,
    ulong EntityId,
    IReadOnlyList<MessageElem> Elems);
