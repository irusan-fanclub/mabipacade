using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;

namespace Mabipacade.Core.Recording;

public sealed class PcapWriter : IDisposable
{
    private readonly CaptureFileWriterDevice _writer;
    private readonly LinkLayers _linkLayer;

    public PcapWriter(string path, LinkLayers linkLayer)
    {
        _linkLayer = linkLayer;
        _writer = new CaptureFileWriterDevice(path, System.IO.FileMode.Create);
        _writer.Open(new DeviceConfiguration { LinkLayerType = linkLayer });
    }

    public void Write(byte[] frame, DateTime timestampUtc)
    {
        var timeval = new PosixTimeval(timestampUtc);
        var raw = new RawCapture(_linkLayer, timeval, frame);
        _writer.Write(raw);
    }

    public void Dispose() => _writer.Close();
}
