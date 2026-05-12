using Mabipacade.Core.Plugins;

namespace Mabipacade.Core.Pipeline;

public sealed class DecoderRegistry
{
    private readonly Dictionary<ushort, IPacketDecoder> _map = new();

    public void Register(IPacketDecoder decoder) => _map[decoder.Op] = decoder;

    public bool TryGet(ushort op, out IPacketDecoder? decoder) => _map.TryGetValue(op, out decoder);

    public IReadOnlyCollection<ushort> RegisteredOps => _map.Keys;
}
