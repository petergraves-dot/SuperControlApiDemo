using System.Text.Json.Serialization;

namespace petergraves.Features.SuperControlDemo;

public sealed class SuperControlDemoRefreshCacheRequest
{
    public string? CacheRefreshCadence { get; init; }

    [JsonPropertyName("__RequestVerificationToken")]
    public string? RequestVerificationToken { get; init; }
}