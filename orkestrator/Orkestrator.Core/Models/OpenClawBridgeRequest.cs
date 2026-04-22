using System.Text.Json.Serialization;

namespace Orkestrator.Models;

public sealed class OpenClawBridgeRequest
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "agent";

    [JsonPropertyName("profile")]
    public AgentProfile? Profile { get; set; }

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("sessionKey")]
    public string? SessionKey { get; set; }

    [JsonPropertyName("timeoutSeconds")]
    public int? TimeoutSeconds { get; set; }
}
