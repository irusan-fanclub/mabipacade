using System.Text.Encodings.Web;
using System.Text.Json;

namespace Mabipacade.Core.Json;

/// <summary>
/// Shared JSON settings. Every writer in the project uses these so the same
/// packet reads the same way in the CLI, the WebSocket server and the debug UI.
/// </summary>
public static class MabiJson
{
    /// <summary>
    /// Emits text verbatim instead of as <c>\uXXXX</c>. Mabinogi's payloads are
    /// mostly CJK — character, item and quest text — and escaping makes the
    /// output unreadable in a terminal, a diff, or a log. Quest reward strings
    /// also carry <c>&lt;color=2&gt;</c> markup that the default encoder escapes.
    ///
    /// "Unsafe" here means HTML-sensitive characters pass through unescaped, so
    /// this output must be encoded like any other untrusted text before being
    /// embedded in a web page. It is written to files, pipes and terminals.
    /// </summary>
    public static JavaScriptEncoder Encoder => JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

    public static JsonWriterOptions WriterOptions(bool indented = false) => new()
    {
        Indented = indented,
        Encoder = Encoder,
    };

    public static JsonSerializerOptions SerializerOptions(bool indented = false) => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = indented,
        Encoder = Encoder,
    };
}
