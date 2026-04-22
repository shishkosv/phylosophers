namespace Orkestrator.Models;

public enum RouteAction
{
    SelectSpeaker,
    Summarize,
    Silence,
    Close
}

public sealed class RouteDecision
{
    public RouteAction Action { get; init; } = RouteAction.Silence;
    public AgentProfile Speaker { get; init; } = AgentProfile.None;
    public string Reason { get; init; } = string.Empty;
    public int MaxWords { get; init; } = 110;
    public bool RequiresContrastSpeaker { get; init; }
}
