using Orkestrator.Models;

namespace Orkestrator.Orchestration;

public sealed class ContrastPolicy
{
    public bool ShouldAddContrast(
        AgentProfile firstSpeaker,
        string firstReply,
        RoomMessage userMessage,
        AgentProfile lastSpeaker)
    {
        var contrastSpeaker = RoomRules.GetContrastSpeaker(firstSpeaker);
        if (!RoomRules.CanSpeak(contrastSpeaker, lastSpeaker))
        {
            return false;
        }

        if (firstReply.Length < 180)
        {
            return false;
        }

        return RoomRules.SupportsTopic(contrastSpeaker, userMessage.Text);
    }
}
