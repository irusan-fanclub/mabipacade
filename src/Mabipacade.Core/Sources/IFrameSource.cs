using Mabipacade.Core.Model;

namespace Mabipacade.Core.Sources;

public interface IFrameSource : IDisposable
{
    event EventHandler<RawFrameEventArgs>? FrameReceived;
    event EventHandler? EndOfStream;
    Task StartAsync(CancellationToken ct);
    Task StopAsync();
}

public sealed class RawFrameEventArgs(byte[] data, PacketDotNet.LinkLayers linkLayer, DateTime timestampUtc) : EventArgs
{
    public byte[] Data { get; } = data;
    public PacketDotNet.LinkLayers LinkLayer { get; } = linkLayer;
    public DateTime TimestampUtc { get; } = timestampUtc;
}
