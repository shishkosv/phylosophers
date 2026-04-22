using System.Text.Json.Serialization;

namespace Orkestrator.Models;

public sealed class OpenClawBridgeResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("profile")]
    public AgentProfile? Profile { get; set; }

    [JsonPropertyName("sessionKey")]
    public string? SessionKey { get; set; }

    [JsonPropertyName("reply")]
    public string? Reply { get; set; }

    [JsonPropertyName("raw")]
    public string? Raw { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
