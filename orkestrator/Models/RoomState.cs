namespace Orkestrator.Models;

public sealed class RoomState
{
    public string RoomId { get; init; } = "philosophers-room";
    public List<RoomMessage> History { get; init; } = new();
    public AgentProfile LastSpeaker { get; set; } = AgentProfile.None;
    public string? LastTurnId { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
