using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mabipacade.Cli.Recording;

internal sealed record SessionMetadata(
    string Id,
    DateTime StartedAt,
    DateTime? EndedAt,
    string Region,
    IReadOnlyList<SessionEndpointRecord> Endpoints,
    SessionStats Stats)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string ToJson(SessionMetadata m) => JsonSerializer.Serialize(m, Options);
}

internal sealed record SessionEndpointRecord(string Remote, DateTime From, DateTime? To);

internal sealed record SessionStats(long TotalFrames, long TotalPackets, long BadBodyCount, long FramingResyncCount);
