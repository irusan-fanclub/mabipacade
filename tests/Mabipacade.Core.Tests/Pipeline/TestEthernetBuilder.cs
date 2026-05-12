using System.Net;
using System.Net.NetworkInformation;
using PacketDotNet;

namespace Mabipacade.Core.Tests.Pipeline;

internal static class TestEthernetBuilder
{
    public static byte[] WrapTcp(byte[] payload, ushort srcPort, ushort dstPort,
        string srcIp = "10.0.0.1", string dstIp = "10.0.0.2")
    {
        var tcp = new TcpPacket(srcPort, dstPort)
        {
            PayloadData = payload,
            SequenceNumber = 1000,
        };
        var ip = new IPv4Packet(IPAddress.Parse(srcIp), IPAddress.Parse(dstIp))
        {
            PayloadPacket = tcp,
            Protocol = ProtocolType.Tcp,
        };
        var eth = new EthernetPacket(
            new PhysicalAddress(new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 }),
            new PhysicalAddress(new byte[] { 0x00, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE }),
            EthernetType.IPv4)
        {
            PayloadPacket = ip,
        };
        return eth.Bytes;
    }
}
