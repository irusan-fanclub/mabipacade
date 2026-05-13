using System.CommandLine;
using Mabipacade.Core.Pipeline;
using Mabipacade.Core.Sources;
using Mabipacade.Decoders;
using Mabipacade.Server.Logging;
using Mabipacade.Server.WebSocket;

namespace Mabipacade.Server.Commands;

internal static class ServeReplayCommand
{
    public static Command Build()
    {
        var port = new Option<int>("--port") { Description = "WS port (default 9876)", DefaultValueFactory = _ => 9876 };
        var input = new Option<FileInfo>("--in") { Description = "Pcap file to replay", Required = true };

        var cmd = new Command("replay", "Serve a pcap replay over WebSocket")
        {
            port, input
        };
        cmd.SetAction(parse => Run(parse.GetValue(port), parse.GetValue(input)!));
        return cmd;
    }

    private static int Run(int port, FileInfo input)
    {
        if (!input.Exists)
        {
            StderrLogger.Error($"Input file does not exist: {input.FullName}");
            return 2;
        }

        using var host = new WebSocketHost(port, OpCodeNames.TryGetName);
        host.Start();

        using var source = new PcapFileFrameSource(input.FullName);
        var registry = new DecoderRegistry();
        DefaultDecoders.RegisterAll(registry);
        var pipeline = new PacketPipeline(source, registry);

        pipeline.PacketReceived += (_, p) => host.BroadcastPacket(p);
        pipeline.SessionEventReceived += (_, ev) => host.BroadcastEvent(ev);

        StderrLogger.Info($"replaying {input.Name} over ws://127.0.0.1:{port}");
        pipeline.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

        // Replay finishes when pcap ends; then we stay listening for late clients.
        StderrLogger.Info("Press Ctrl+C to stop.");
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
        try { Task.Delay(Timeout.Infinite, cts.Token).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { }

        pipeline.StopAsync().GetAwaiter().GetResult();
        return 0;
    }
}
