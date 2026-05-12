using System.Diagnostics;

namespace Mabipacade.Core.Capture;

public sealed class ProcessFinder
{
    public int? Find(string processName)
    {
        var procs = Process.GetProcessesByName(processName);
        try
        {
            return procs.Length > 0 ? procs[0].Id : null;
        }
        finally
        {
            foreach (var p in procs) p.Dispose();
        }
    }
}
