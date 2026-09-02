using System.CommandLine;
using Mabipacade.Cli.Filters;
using Mabipacade.Cli.Logging;
using Mabipacade.Core.Json;
using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Sources;
using Mabipacade.Decoders;

namespace Mabipacade.Cli.Commands;

internal static class ReplayCommand
{
    public static Command Build()
    {
        // No extension whitelist: libpcap identifies the format from the file's
        // magic bytes, so .cap and extensionless captures open too.
        var input = new Option<FileInfo>("--in") { Description = "Capture file to replay (pcap or pcapng)", Required = true };
        var filterOp = new Option<string?>("--filter-op") { Description = "Comma-separated hex op list, e.g. 0x6984,0x7926" };
        var decodedOnly = new Option<bool>("--decoded-only") { Description = "Skip packets without an L3 decoder" };
        var diagnostics = new Option<string?>("--diagnostics") { Description = "off|on (default off)" };

        var cmd = new Command("replay", "Replay a pcap file and emit NDJSON to stdout")
        {
            input, filterOp, decodedOnly, diagnostics
        };
        cmd.SetAction(parse => Run(
            parse.GetValue(input)!,
            parse.GetValue(filterOp),
            parse.GetValue(decodedOnly),
            DiagnosticsLevel.Parse(parse.GetValue(diagnostics))));
        return cmd;
    }

    private static int Run(FileInfo input, string? filterOpSpec, bool decodedOnly, DiagnosticsLevel diag)
    {
        if (!input.Exists)
        {
            StderrLogger.Error($"Input file does not exist: {input.FullName}");
            return 2;
        }

        OpFilter opFilter;
        try { opFilter = OpFilter.Parse(filterOpSpec); }
        catch (FormatException e) { StderrLogger.Error(e.Message); return 2; }

        var writer = new NdjsonWriter(Console.Out, OpCodeNames.TryGetName);
        using var source = new PcapFileFrameSource(input.FullName);
        var registry = new DecoderRegistry();
        DefaultDecoders.RegisterAll(registry);
        var pipeline = new PacketPipeline(source, registry);

        pipeline.PacketReceived += (_, p) =>
        {
            if (!opFilter.Allows(p.Op)) return;
            if (decodedOnly && p.Decoded is null) return;
            writer.WritePacket(p);
        };
        pipeline.SessionEventReceived += (_, ev) =>
        {
            if (!diag.PassesLive(ev)) return;
            writer.WriteEvent(ev);
        };

        StderrLogger.Info($"Replaying {input.Name}…");
        pipeline.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        pipeline.StopAsync().GetAwaiter().GetResult();
        var summary = $"Done. Packets: {pipeline.Metrics.TotalPackets}, BadBody: {pipeline.Metrics.BadBodyCount}";
        if (pipeline.Metrics.MalformedFrameCount > 0)
            summary += $", MalformedFrames: {pipeline.Metrics.MalformedFrameCount}";
        // Not an error — the file simply ends mid-record, which is normal for a
        // recorder that was killed. Said plainly so a short read isn't mistaken
        // for a complete one.
        if (source.Truncated) summary += " (truncated tail)";
        StderrLogger.Info(summary);
        return 0;
    }
}
