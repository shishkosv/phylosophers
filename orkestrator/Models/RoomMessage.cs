namespace Orkestrator.Models;

public sealed class RoomMessage
{
    public required string Id { get; init; }
    public required string SenderId { get; init; }
    public required string SenderName { get; init; }
    public required string Text { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
    public bool IsHuman { get; init; } = true;
    public string? ReplyToMessageId { get; init; }
    public AgentProfile? ProducedBy { get; init; }
}
