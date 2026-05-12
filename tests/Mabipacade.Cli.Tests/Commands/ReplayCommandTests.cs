using System.Diagnostics;
using System.Text.Json;

namespace Mabipacade.Cli.Tests.Commands;

public class ReplayCommandTests
{
    private static readonly string FixturePath = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..",
        "Mabipacade.Core.Tests", "fixtures", "known_good.pcap");

    private static string CliProjectPath => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "src", "Mabipacade.Cli", "Mabipacade.Cli.csproj"));

    [Fact(Skip = "Requires local fixture — copy a pcap to tests/Mabipacade.Core.Tests/fixtures/known_good.pcap")]
    public void Replay_EmitsValidNdjsonLines_ForRealPcap()
    {
        if (!File.Exists(FixturePath)) return;

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{CliProjectPath}\" -- replay --in \"{FixturePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var proc = Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(60_000);

        Assert.Equal(0, proc.ExitCode);

        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length > 0, $"expected at least one NDJSON line. stderr:\n{stderr}");

        int packetCount = 0;
        foreach (var line in lines)
        {
            var doc = JsonDocument.Parse(line);
            Assert.True(doc.RootElement.TryGetProperty("kind", out var kind));
            var k = kind.GetString();
            Assert.True(k == "packet" || k == "event", $"unexpected kind '{k}'");
            if (k == "packet")
            {
                packetCount++;
                Assert.True(doc.RootElement.TryGetProperty("op", out _));
                Assert.True(doc.RootElement.TryGetProperty("elems", out _));
            }
        }
        Assert.True(packetCount > 0, "expected at least one packet line");
    }
}
