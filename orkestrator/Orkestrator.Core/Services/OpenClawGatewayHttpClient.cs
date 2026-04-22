using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Orkestrator.Config;

namespace Orkestrator.Services;

public sealed class OpenClawGatewayHttpClient : IOpenClawGatewayClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly OrchestratorOptions _options;

    public OpenClawGatewayHttpClient(HttpClient httpClient, IOptions<OrchestratorOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _httpClient.BaseAddress = new Uri(_options.OpenClaw.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.OpenClaw.TimeoutSeconds);
    }

    public async Task<string?> SendAsync(string sessionKey, string prompt, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            tool = "sessions_send",
            args = new
            {
                sessionKey,
                message = prompt,
                timeoutSeconds = _options.OpenClaw.TimeoutSeconds
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/tools/invoke")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(_options.OpenClaw.BearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.OpenClaw.BearerToken);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        return ExtractResultText(body);
    }

    private static string ExtractResultText(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.False)
        {
            var errorMessage = root.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.String
                    ? message.GetString()
                    : "OpenClaw tool invocation failed.";

            throw new InvalidOperationException(errorMessage);
        }

        if (!root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object)
        {
            return body;
        }

        if (result.TryGetProperty("details", out var details))
        {
            return details.GetRawText();
        }

        if (result.TryGetProperty("content", out var content)
            && content.ValueKind == JsonValueKind.Array
            && content.GetArrayLength() > 0)
        {
            var first = content[0];
            if (first.ValueKind == JsonValueKind.Object
                && first.TryGetProperty("text", out var text)
                && text.ValueKind == JsonValueKind.String)
            {
                return text.GetString() ?? body;
            }
        }

        return result.GetRawText();
    }
}
