using Orkestrator.Models;

namespace Orkestrator.Orchestration;

public static class RoomRules
{
    public const int MaxRepliesPerUserTurn = 2;

    public static bool CanSpeak(AgentProfile candidate, AgentProfile lastSpeaker)
        => candidate != AgentProfile.None && candidate != lastSpeaker;

    public static bool SupportsTopic(AgentProfile profile, string text)
    {
        var lower = text.ToLowerInvariant();
        return profile switch
        {
            AgentProfile.Freud => ContainsAny(lower, "motive", "jealous", "shame", "contradict", "irrational", "repress", "hidden"),
            AgentProfile.Marcus => ContainsAny(lower, "anger", "fear", "discipline", "stress", "control", "uncertain", "outcome"),
            AgentProfile.Aristotle => ContainsAny(lower, "virtue", "justice", "habit", "friendship", "courage", "moderation", "ethic"),
            _ => false
        };
    }

    public static AgentProfile GetContrastSpeaker(AgentProfile profile)
        => profile switch
        {
            AgentProfile.Freud => AgentProfile.Aristotle,
            AgentProfile.Aristotle => AgentProfile.Marcus,
            AgentProfile.Marcus => AgentProfile.Aristotle,
            _ => AgentProfile.None
        };

    private static bool ContainsAny(string value, params string[] needles)
        => needles.Any(value.Contains);
}
