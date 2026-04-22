namespace Orkestrator.Models;

public sealed class OrchestrationTurnResult
{
    public List<RoomMessage> PublishedMessages { get; init; } = new();
    public bool PublishedSummary { get; init; }
    public bool StayedSilent { get; init; }
}
