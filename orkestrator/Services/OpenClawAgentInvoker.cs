using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orkestrator.Config;
using Orkestrator.Models;

namespace Orkestrator.Services;

public sealed class OpenClawAgentInvoker : IAgentInvoker
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenClawAgentInvoker> _logger;
    private readonly OrchestratorOptions _options;

    public OpenClawAgentInvoker(HttpClient httpClient, IOptions<OrchestratorOptions> options, ILogger<OpenClawAgentInvoker> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
        _httpClient.BaseAddress = new Uri(_options.OpenClaw.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.OpenClaw.TimeoutSeconds);
    }

    public async Task<string?> InvokeAsync(AgentProfile profile, IReadOnlyList<RoomMessage> history, RoomMessage userMessage, int maxWords, CancellationToken cancellationToken = default)
    {
        var prompt = BuildAgentPrompt(profile, history, userMessage, maxWords);
        _logger.LogInformation("Invoking agent {Profile}", profile);

        if (string.IsNullOrWhiteSpace(_options.OpenClaw.SessionKey))
        {
            return $"[{profile}] {prompt}";
        }

        var payload = new
        {
            sessionKey = _options.OpenClaw.SessionKey,
            message = prompt,
            timeoutSeconds = _options.OpenClaw.TimeoutSeconds
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/api/sessions/send", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        return responseText;
    }

    public async Task<RouteDecision> InvokeModeratorAsync(IReadOnlyList<RoomMessage> history, RoomMessage userMessage, CancellationToken cancellationToken = default)
    {
        var prompt = BuildModeratorPrompt(history, userMessage);
        _logger.LogInformation("Invoking moderator selector");

        if (string.IsNullOrWhiteSpace(_options.OpenClaw.SessionKey))
        {
            return new RouteDecision
            {
                Action = RouteAction.SelectSpeaker,
                Speaker = GuessSpeaker(userMessage.Text),
                Reason = "Fallback local routing",
                MaxWords = 110
            };
        }

        var payload = new
        {
            sessionKey = _options.OpenClaw.SessionKey,
            message = prompt,
            timeoutSeconds = _options.OpenClaw.TimeoutSeconds
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/api/sessions/send", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            var decision = JsonSerializer.Deserialize<RouteDecision>(responseText, JsonOptions);
            return decision ?? new RouteDecision { Action = RouteAction.Silence, Reason = "Empty moderator decision" };
        }
        catch
        {
            return new RouteDecision
            {
                Action = RouteAction.SelectSpeaker,
                Speaker = GuessSpeaker(userMessage.Text),
                Reason = "Moderator response parse failure fallback",
                MaxWords = 110
            };
        }
    }

    private static AgentProfile GuessSpeaker(string text)
    {
        var lower = text.ToLowerInvariant();
        if (lower.Contains("shame") || lower.Contains("jealous") || lower.Contains("contradict") || lower.Contains("motive")) return AgentProfile.Freud;
        if (lower.Contains("anger") || lower.Contains("fear") || lower.Contains("discipline") || lower.Contains("control")) return AgentProfile.Marcus;
        return AgentProfile.Aristotle;
    }

    private static string BuildAgentPrompt(AgentProfile profile, IReadOnlyList<RoomMessage> history, RoomMessage userMessage, int maxWords)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Reply as {profile}.");
        sb.AppendLine($"Hard cap: {maxWords} words.");
        sb.AppendLine("Recent room history:");
        foreach (var message in history.TakeLast(8))
        {
            sb.AppendLine($"- {message.SenderName}: {message.Text}");
        }
        sb.AppendLine($"Current user message: {userMessage.Text}");
        return sb.ToString();
    }

    private static string BuildModeratorPrompt(IReadOnlyList<RoomMessage> history, RoomMessage userMessage)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Return JSON only.");
        sb.AppendLine("Select speaker, summarize, silence, or close.");
        sb.AppendLine("Recent room history:");
        foreach (var message in history.TakeLast(10))
        {
            sb.AppendLine($"- {message.SenderName}: {message.Text}");
        }
        sb.AppendLine($"Current user message: {userMessage.Text}");
        return sb.ToString();
    }
}
