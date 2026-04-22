using Orkestrator.Models;

namespace Orkestrator.Services;

public interface IAgentInvoker
{
    Task<string?> InvokeAsync(
        AgentProfile profile,
        IReadOnlyList<RoomMessage> history,
        RoomMessage userMessage,
        int maxWords,
        CancellationToken cancellationToken = default);

    Task<RouteDecision> InvokeModeratorAsync(
        IReadOnlyList<RoomMessage> history,
        RoomMessage userMessage,
        CancellationToken cancellationToken = default);
}
