using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orkestrator.Config;
using Orkestrator.Models;

namespace Orkestrator.Services;

public sealed class OpenClawBridgeWorker
{
    private readonly IOpenClawGatewayClient _gatewayClient;
    private readonly ILogger<OpenClawBridgeWorker> _logger;
    private readonly OrchestratorOptions _options;

    public OpenClawBridgeWorker(
        IOpenClawGatewayClient gatewayClient,
        IOptions<OrchestratorOptions> options,
        ILogger<OpenClawBridgeWorker> logger)
    {
        _gatewayClient = gatewayClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<OpenClawBridgeResponse> ProcessAsync(OpenClawBridgeRequest request, CancellationToken cancellationToken = default)
    {
        var sessionKey = string.IsNullOrWhiteSpace(request.SessionKey)
            ? _options.OpenClaw.SessionKey
            : request.SessionKey;

        _logger.LogInformation("Processing bridge request. Kind={Kind}, Profile={Profile}, RequestSessionKeyPresent={RequestSessionKeyPresent}, EffectiveSessionKeyPresent={EffectiveSessionKeyPresent}, PromptLength={PromptLength}",
            request.Kind,
            request.Profile,
            !string.IsNullOrWhiteSpace(request.SessionKey),
            !string.IsNullOrWhiteSpace(sessionKey),
            request.Prompt?.Length ?? 0);

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            _logger.LogWarning("Rejecting bridge request because prompt is empty");
            return new OpenClawBridgeResponse
            {
                Ok = false,
                Kind = request.Kind,
                Profile = request.Profile,
                SessionKey = sessionKey,
                Error = "Prompt is required."
            };
        }

        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            if (_options.OpenClaw.EnablePromptEchoFallback)
            {
                _logger.LogInformation("Using bridge worker fallback for {Kind}/{Profile}", request.Kind, request.Profile);
                var fallback = $"[{request.Profile ?? AgentProfile.Moderator}] {request.Prompt}";
                return new OpenClawBridgeResponse
                {
                    Ok = true,
                    Kind = request.Kind,
                    Profile = request.Profile,
                    SessionKey = sessionKey,
                    Reply = fallback,
                    Raw = JsonSerializer.Serialize(new { reply = fallback })
                };
            }

            _logger.LogWarning("Rejecting bridge request because no session key is available and fallback is disabled");
            return new OpenClawBridgeResponse
            {
                Ok = false,
                Kind = request.Kind,
                Profile = request.Profile,
                Error = "SessionKey is required when fallback is disabled."
            };
        }

        try
        {
            var raw = await _gatewayClient.SendAsync(sessionKey, request.Prompt, cancellationToken);
            _logger.LogInformation("Bridge request completed successfully. Kind={Kind}, Profile={Profile}, RawLength={RawLength}", request.Kind, request.Profile, raw?.Length ?? 0);
            return new OpenClawBridgeResponse
            {
                Ok = true,
                Kind = request.Kind,
                Profile = request.Profile,
                SessionKey = sessionKey,
                Reply = ExtractAssistantText(raw) ?? raw,
                Raw = raw
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bridge request failed. Kind={Kind}, Profile={Profile}, SessionKey={SessionKey}", request.Kind, request.Profile, sessionKey);
            return new OpenClawBridgeResponse
            {
                Ok = false,
                Kind = request.Kind,
                Profile = request.Profile,
                SessionKey = sessionKey,
                Error = ex.Message
            };
        }
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
}
