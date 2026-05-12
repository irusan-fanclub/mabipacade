using Mabipacade.Core.Recording;
using PacketDotNet;
using SharpPcap.LibPcap;

namespace Mabipacade.Core.Tests.Recording;

public class PcapWriterTests
{
    [Fact]
    public void WrittenFrames_AreReadableBack()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mabipacade-test-{Guid.NewGuid():N}.pcap");
        try
        {
            using (var writer = new PcapWriter(path, LinkLayers.Ethernet))
            {
                writer.Write(new byte[] { 1, 2, 3, 4 }, DateTime.UtcNow);
                writer.Write(new byte[] { 5, 6, 7, 8 }, DateTime.UtcNow);
            }
            int read = 0;
            using var reader = new CaptureFileReaderDevice(path);
            reader.Open(new SharpPcap.DeviceConfiguration());
            reader.OnPacketArrival += (_, _) => read++;
            reader.Capture();
            reader.Close();
            Assert.Equal(2, read);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
