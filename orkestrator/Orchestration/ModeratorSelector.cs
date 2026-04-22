using Orkestrator.Models;
using Orkestrator.Services;

namespace Orkestrator.Orchestration;

public sealed class ModeratorSelector
{
    private readonly IAgentInvoker _agentInvoker;

    public ModeratorSelector(IAgentInvoker agentInvoker)
    {
        _agentInvoker = agentInvoker;
    }

    public Task<RouteDecision> SelectAsync(IReadOnlyList<RoomMessage> history, RoomMessage userMessage, CancellationToken cancellationToken = default)
        => _agentInvoker.InvokeModeratorAsync(history, userMessage, cancellationToken);
}
