using System.Text.Json;
using Mabipacade.Core.Diagnostics;
using Mabipacade.Core.Model;

namespace Mabipacade.Cli.Output;

internal sealed class NdjsonWriter
{
    private readonly TextWriter _out;
    private readonly object _lock = new();

    public NdjsonWriter(TextWriter outWriter) { _out = outWriter; }

    public void WritePacket(MabiPacket p) => WriteLine(w => EnvelopeShape.WritePacket(w, p));
    public void WriteEvent(SessionEvent ev) => WriteLine(w => EnvelopeShape.WriteEvent(w, ev));

    private void WriteLine(Action<Utf8JsonWriter> writeBody)
    {
        using var ms = new MemoryStream();
        using (var jw = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false }))
        {
            writeBody(jw);
        }
        var line = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        lock (_lock)
        {
            _out.Write(line);
            _out.Write('\n');
        }
    }
}
