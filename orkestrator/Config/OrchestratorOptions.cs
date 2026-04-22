namespace Orkestrator.Config;

public sealed class OrchestratorOptions
{
    public string RoomId { get; set; } = "philosophers-room";
    public int HistoryLimit { get; set; } = 50;
    public int MaxPhilosopherRepliesPerTurn { get; set; } = 2;
    public int ModeratorSummaryWords { get; set; } = 90;
    public double SimilarityThreshold { get; set; } = 0.72;
    public string StateFilePath { get; set; } = "memory/room-state.json";
    public OpenClawOptions OpenClaw { get; set; } = new();
    public TelegramOptions Telegram { get; set; } = new();
}

public sealed class OpenClawOptions
{
    public string BaseUrl { get; set; } = "http://localhost:18789";
    public string SessionKey { get; set; } = string.Empty;
    public string EndpointPath { get; set; } = string.Empty;
    public string BearerToken { get; set; } = string.Empty;
    public bool EnablePromptEchoFallback { get; set; }
    public int TimeoutSeconds { get; set; } = 60;
}

public sealed class TelegramOptions
{
    public string BotToken { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public bool UseWebhook { get; set; }
    public string WebhookSecret { get; set; } = string.Empty;
}
