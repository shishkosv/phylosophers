using Orkestrator.Models;

namespace Orkestrator.Services;

public interface IOpenClawBridge
{
    Task<string?> SendAgentPromptAsync(
        AgentProfile profile,
        string prompt,
        CancellationToken cancellationToken = default);

    Task<string?> SendModeratorPromptAsync(
        string prompt,
        CancellationToken cancellationToken = default);
}
