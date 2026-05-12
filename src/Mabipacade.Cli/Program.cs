using System.CommandLine;

namespace Mabipacade.Cli;

public static class Program
{
    public static Task<int> Main(string[] args)
    {
        var root = new RootCommand("Mabipacade CLI — Mabinogi packet sidecar");
        return root.Parse(args).InvokeAsync();
    }
}
