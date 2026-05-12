using System.Net;
using SharpPcap;

namespace Mabipacade.Core.Capture;

public interface INicSelector
{
    ICaptureDevice? SelectFor(IPAddress remote);
}
