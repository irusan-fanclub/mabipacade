using System.CommandLine;
using Mabipacade.Cli.Commands;

namespace Mabipacade.Cli;

public static class Program
{
    public static Task<int> Main(string[] args)
    {
        // The NDJSON carries CJK text verbatim. Without this the console's
        // legacy code page mangles it on the way out, whether it goes to a
        // terminal or is redirected to a file. No BOM: the output is a stream of
        // lines meant to be piped, and a BOM breaks the first line for consumers.
        Console.OutputEncoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        var root = new RootCommand("Mabipacade CLI — Mabinogi packet sidecar")
        {
            ReplayCommand.Build(),
            CaptureCommand.Build()
        };
        return root.Parse(args).InvokeAsync();
    }
}
