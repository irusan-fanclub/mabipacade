using System.Net;
using System.Runtime.InteropServices;

namespace Mabipacade.Core.Capture;

public sealed class Win32TcpConnectionTable : ITcpConnectionTable
{
    public IReadOnlyList<TcpConnectionRow> GetConnections()
    {
        const int AF_INET = 2;
        const int TCP_TABLE_OWNER_PID_ALL = 5;

        int bufSize = 0;
        _ = GetExtendedTcpTable(IntPtr.Zero, ref bufSize, false, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
        var ptr = Marshal.AllocHGlobal(bufSize);
        try
        {
            int rc = GetExtendedTcpTable(ptr, ref bufSize, false, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
            if (rc != 0) throw new InvalidOperationException($"GetExtendedTcpTable rc={rc}");

            int rowCount = Marshal.ReadInt32(ptr);
            var rows = new List<TcpConnectionRow>(rowCount);
            IntPtr rowPtr = IntPtr.Add(ptr, 4);
            int rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
            for (int i = 0; i < rowCount; i++)
            {
                var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr);
                rows.Add(new TcpConnectionRow(
                    new IPAddress(row.localAddr),
                    (ushort)IPAddress.NetworkToHostOrder((short)row.localPort),
                    new IPAddress(row.remoteAddr),
                    (ushort)IPAddress.NetworkToHostOrder((short)row.remotePort),
                    MapState(row.state),
                    row.owningPid));
                rowPtr = IntPtr.Add(rowPtr, rowSize);
            }
            return rows;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static TcpConnectionState MapState(uint s) => s switch
    {
        1 => TcpConnectionState.Closed,
        2 => TcpConnectionState.Listen,
        3 => TcpConnectionState.SynSent,
        4 => TcpConnectionState.SynReceived,
        5 => TcpConnectionState.Established,
        6 => TcpConnectionState.FinWait1,
        7 => TcpConnectionState.FinWait2,
        8 => TcpConnectionState.CloseWait,
        9 => TcpConnectionState.Closing,
        10 => TcpConnectionState.LastAck,
        11 => TcpConnectionState.TimeWait,
        12 => TcpConnectionState.DeleteTcb,
        _ => TcpConnectionState.Unknown
    };

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern int GetExtendedTcpTable(
        IntPtr pTcpTable, ref int dwOutBufLen, bool sort,
        int ipVersion, int tableClass, uint reserved);

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID
    {
        public uint state;
        public uint localAddr;
        public uint localPort;
        public uint remoteAddr;
        public uint remotePort;
        public int owningPid;
    }
}
