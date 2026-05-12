using System.Text.Json;
using Mabipacade.Cli.Recording;

namespace Mabipacade.Cli.Tests.Recording;

public class SessionMetadataTests
{
    [Fact]
    public void Serializes_WithCamelCase_AndIndented()
    {
        var meta = new SessionMetadata(
            Id: "2026-05-13T18-23-11",
            StartedAt: new DateTime(2026, 5, 13, 18, 23, 11, DateTimeKind.Utc),
            EndedAt: new DateTime(2026, 5, 13, 18, 50, 0, DateTimeKind.Utc),
            Region: "tw",
            Endpoints: new[]
            {
                new SessionEndpointRecord("61.218.1.2:11000",
                    new DateTime(2026, 5, 13, 18, 23, 11, DateTimeKind.Utc),
                    new DateTime(2026, 5, 13, 18, 45, 12, DateTimeKind.Utc))
            },
            Stats: new SessionStats(184231, 92117, 14, 2));

        var json = SessionMetadata.ToJson(meta);
        Assert.Contains("\"id\": \"2026-05-13T18-23-11\"", json);
        Assert.Contains("\"startedAt\": \"2026-05-13T18:23:11", json);
        Assert.Contains("\"region\": \"tw\"", json);
        Assert.Contains("\"totalFrames\": 184231", json);
        Assert.Contains("\"badBodyCount\": 14", json);
    }
}
