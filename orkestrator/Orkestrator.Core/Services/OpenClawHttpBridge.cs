using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Orkestrator.Config;
using Orkestrator.Models;

namespace Orkestrator.Services;

public sealed class OpenClawHttpBridge : IOpenClawBridge
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly OrchestratorOptions _options;

    public OpenClawHttpBridge(HttpClient httpClient, IOptions<OrchestratorOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _httpClient.BaseAddress = new Uri(_options.OpenClaw.InternalBridge.Url);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.OpenClaw.TimeoutSeconds);
    }

    public Task<string?> SendAgentPromptAsync(AgentProfile profile, string prompt, CancellationToken cancellationToken = default)
        => PostPromptAsync(new OpenClawBridgeRequest
        {
            Kind = "agent",
            Profile = profile,
            Prompt = prompt,
            SessionKey = _options.OpenClaw.SessionKey,
            TimeoutSeconds = _options.OpenClaw.TimeoutSeconds
        }, cancellationToken);

    public Task<string?> SendModeratorPromptAsync(string prompt, CancellationToken cancellationToken = default)
        => PostPromptAsync(new OpenClawBridgeRequest
        {
            Kind = "moderator",
            Profile = AgentProfile.Moderator,
            Prompt = prompt,
            SessionKey = _options.OpenClaw.SessionKey,
            TimeoutSeconds = _options.OpenClaw.TimeoutSeconds
        }, cancellationToken);

    private async Task<string?> PostPromptAsync(OpenClawBridgeRequest payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.OpenClaw.InternalBridge.RoutePath)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(_options.OpenClaw.BearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.OpenClaw.BearerToken);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            var bridgeResponse = JsonSerializer.Deserialize<OpenClawBridgeResponse>(content, JsonOptions);
            return bridgeResponse?.Raw ?? bridgeResponse?.Reply ?? content;
        }
        catch (JsonException)
        {
            return content;
        }
    }
}
