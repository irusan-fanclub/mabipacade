using Mabipacade.Core.Plugins;

namespace Mabipacade.Core.Pipeline;

public sealed class DecoderRegistry
{
    private readonly Dictionary<uint, IPacketDecoder> _map = new();

    public void Register(IPacketDecoder decoder) => _map[decoder.Op] = decoder;

    public bool TryGet(uint op, out IPacketDecoder? decoder) => _map.TryGetValue(op, out decoder);

    public IReadOnlyCollection<uint> RegisteredOps => _map.Keys;
}
