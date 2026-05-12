using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using SharpPcap;
using SharpPcap.LibPcap;

namespace Mabipacade.Core.Capture;

public sealed class Win32NicSelector : INicSelector
{
    public ICaptureDevice? SelectFor(IPAddress remote)
    {
        if (GetBestInterface(BitConverter.ToUInt32(remote.GetAddressBytes(), 0), out uint ifIndex) != 0)
            return null;

        var winNic = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(n => n.GetIPProperties().GetIPv4Properties()?.Index == ifIndex);
        if (winNic is null) return null;

        foreach (LibPcapLiveDevice d in LibPcapLiveDeviceList.Instance)
        {
            if (d.Interface?.FriendlyName is { } friendly && friendly == winNic.Name)
                return d;
        }
        return null;
    }

    [DllImport("iphlpapi.dll", CharSet = CharSet.Auto)]
    private static extern int GetBestInterface(uint destAddr, out uint bestIfIndex);
}
