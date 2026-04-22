using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orkestrator.Config;
using Orkestrator.Models;

namespace Orkestrator.Services;

public sealed class OpenClawLocalBridge : IOpenClawBridge
{
    private readonly OpenClawHttpBridge _httpBridge;
    private readonly ILogger<OpenClawLocalBridge> _logger;
    private readonly OrchestratorOptions _options;

    public OpenClawLocalBridge(OpenClawHttpBridge httpBridge, IOptions<OrchestratorOptions> options, ILogger<OpenClawLocalBridge> logger)
    {
        _httpBridge = httpBridge;
        _logger = logger;
        _options = options.Value;
    }

    public Task<string?> SendAgentPromptAsync(AgentProfile profile, string prompt, CancellationToken cancellationToken = default)
        => SendAsync(profile, prompt, cancellationToken);

    public Task<string?> SendModeratorPromptAsync(string prompt, CancellationToken cancellationToken = default)
        => SendAsync(AgentProfile.Moderator, prompt, cancellationToken);

    private async Task<string?> SendAsync(AgentProfile profile, string prompt, CancellationToken cancellationToken)
    {
        if (IsRemoteConfigured())
        {
            return await _httpBridge.SendAgentPromptAsync(profile, prompt, cancellationToken);
        }

        if (_options.OpenClaw.EnablePromptEchoFallback)
        {
            _logger.LogInformation("Using local fallback bridge for {Profile}", profile);
            return JsonSerializer.Serialize(new
            {
                reply = $"[{profile}] {prompt}"
            });
        }

        throw new InvalidOperationException(
            "OpenClaw local bridge is not fully configured. Set Orchestrator:OpenClaw:EndpointPath and SessionKey, or enable EnablePromptEchoFallback for smoke tests.");
    }

    private bool IsRemoteConfigured()
        => !string.IsNullOrWhiteSpace(_options.OpenClaw.EndpointPath)
           && !string.IsNullOrWhiteSpace(_options.OpenClaw.SessionKey);
}
