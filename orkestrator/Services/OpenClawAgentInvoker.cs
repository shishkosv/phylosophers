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

    private readonly IOpenClawBridge _bridge;
    private readonly ILogger<OpenClawAgentInvoker> _logger;
    private readonly OrchestratorOptions _options;

    public OpenClawAgentInvoker(IOpenClawBridge bridge, IOptions<OrchestratorOptions> options, ILogger<OpenClawAgentInvoker> logger)
    {
        _bridge = bridge;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<string?> InvokeAsync(AgentProfile profile, IReadOnlyList<RoomMessage> history, RoomMessage userMessage, int maxWords, CancellationToken cancellationToken = default)
    {
        var prompt = BuildAgentPrompt(profile, history, userMessage, maxWords);
        _logger.LogInformation("Invoking agent {Profile}", profile);

        if (!IsRemoteInvocationConfigured() && _options.OpenClaw.EnablePromptEchoFallback)
        {
            return BuildFallbackResponse(profile, prompt);
        }

        var responseText = await _bridge.SendAgentPromptAsync(profile, prompt, cancellationToken);
        return ExtractAssistantText(responseText) ?? responseText;
    }

    public async Task<RouteDecision> InvokeModeratorAsync(IReadOnlyList<RoomMessage> history, RoomMessage userMessage, CancellationToken cancellationToken = default)
    {
        var prompt = BuildModeratorPrompt(history, userMessage);
        _logger.LogInformation("Invoking moderator selector");

        if (!IsRemoteInvocationConfigured() && _options.OpenClaw.EnablePromptEchoFallback)
        {
            return BuildFallbackDecision(userMessage.Text, "Fallback local routing");
        }

        var responseText = await _bridge.SendModeratorPromptAsync(prompt, cancellationToken);
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return BuildFallbackDecision(userMessage.Text, "Empty moderator response fallback");
        }

        try
        {
            var decision = JsonSerializer.Deserialize<RouteDecision>(responseText, JsonOptions);
            return decision ?? new RouteDecision { Action = RouteAction.Silence, Reason = "Empty moderator decision" };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse moderator response as RouteDecision. Falling back to local selector.");
            return BuildFallbackDecision(userMessage.Text, "Moderator response parse failure fallback");
        }
    }

    private bool IsRemoteInvocationConfigured()
    {
        return !string.IsNullOrWhiteSpace(_options.OpenClaw.InternalBridge.Url)
            && !string.IsNullOrWhiteSpace(_options.OpenClaw.InternalBridge.RoutePath)
            && (!string.IsNullOrWhiteSpace(_options.OpenClaw.SessionKey) || _options.OpenClaw.EnablePromptEchoFallback);
    }

    private string BuildFallbackResponse(AgentProfile profile, string prompt)
    {
        if (_options.OpenClaw.EnablePromptEchoFallback)
        {
            return $"[{profile}] {prompt}";
        }

        throw new InvalidOperationException(
            "OpenClaw agent invocation is not configured. Set Orchestrator:OpenClaw:InternalBridge:Url and SessionKey, or enable EnablePromptEchoFallback for local smoke tests.");
    }

    private RouteDecision BuildFallbackDecision(string userText, string reason)
    {
        if (!_options.OpenClaw.EnablePromptEchoFallback)
        {
            throw new InvalidOperationException(
                "OpenClaw moderator invocation is not configured. Set Orchestrator:OpenClaw:InternalBridge:Url and SessionKey, or enable EnablePromptEchoFallback for local smoke tests.");
        }

        return new RouteDecision
        {
            Action = RouteAction.SelectSpeaker,
            Speaker = GuessSpeaker(userText),
            Reason = reason,
            MaxWords = 110
        };
    }

    private static string? ExtractAssistantText(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return responseText;
        }

        try
        {
            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("reply", out var reply) && reply.ValueKind == JsonValueKind.String)
                {
                    return reply.GetString();
                }

                if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                {
                    return message.GetString();
                }

                if (root.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                {
                    return text.GetString();
                }
            }
        }
        catch (JsonException)
        {
        }

        return null;
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
