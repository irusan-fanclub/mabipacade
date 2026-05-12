using System.CommandLine;
using Mabipacade.Cli.Commands;

namespace Mabipacade.Cli;

public static class Program
{
    public static Task<int> Main(string[] args)
    {
        var root = new RootCommand("Mabipacade CLI — Mabinogi packet sidecar")
        {
            ReplayCommand.Build()
        };
        return root.Parse(args).InvokeAsync();
    }
}
