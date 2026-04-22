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
        if (IsBridgeConfigured())
        {
            _logger.LogDebug("Forwarding {Profile} prompt through internal bridge", profile);
            return await _httpBridge.SendAgentPromptAsync(profile, prompt, cancellationToken);
        }

        if (_options.OpenClaw.EnablePromptEchoFallback)
        {
            _logger.LogInformation("Using local fallback bridge for {Profile}", profile);
            return $"{{\"reply\":\"[{profile}] {Escape(prompt)}\"}}";
        }

        throw new InvalidOperationException(
            "OpenClaw local bridge is not fully configured. Set Orchestrator:OpenClaw:InternalBridge:Url and SessionKey, or enable EnablePromptEchoFallback for smoke tests.");
    }

    private bool IsBridgeConfigured()
        => !string.IsNullOrWhiteSpace(_options.OpenClaw.InternalBridge.Url)
           && !string.IsNullOrWhiteSpace(_options.OpenClaw.InternalBridge.RoutePath);

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
