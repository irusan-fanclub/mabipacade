using System.CommandLine;
using Mabipacade.Server.Commands;

namespace Mabipacade.Server;

public static class Program
{
    public static Task<int> Main(string[] args)
    {
        var root = new RootCommand("Mabipacade WebSocket server — broadcasts Mabinogi packet stream as JSON")
        {
            ServeReplayCommand.Build(),
            ServeCaptureCommand.Build()
        };
        return root.Parse(args).InvokeAsync();
    }
}
