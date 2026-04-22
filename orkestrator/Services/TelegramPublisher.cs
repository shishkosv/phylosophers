using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orkestrator.Config;
using Orkestrator.Models;

namespace Orkestrator.Services;

public sealed class TelegramPublisher : ITelegramPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramPublisher> _logger;
    private readonly OrchestratorOptions _options;

    public TelegramPublisher(HttpClient httpClient, IOptions<OrchestratorOptions> options, ILogger<TelegramPublisher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task PublishAsync(RoomMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Telegram.BotToken) || string.IsNullOrWhiteSpace(_options.Telegram.ChatId))
        {
            _logger.LogInformation("Telegram not configured, skipping publish -> [{Speaker}] {Text}", message.ProducedBy, message.Text);
            return;
        }

        var url = $"https://api.telegram.org/bot{_options.Telegram.BotToken}/sendMessage";
        var payload = new Dictionary<string, object?>
        {
            ["chat_id"] = _options.Telegram.ChatId,
            ["text"] = FormatOutgoingText(message),
            ["disable_web_page_preview"] = _options.Telegram.DisableWebPagePreview
        };

        if (!string.IsNullOrWhiteSpace(message.ReplyToMessageId) && long.TryParse(message.ReplyToMessageId, out var replyToMessageId))
        {
            payload["reply_parameters"] = new Dictionary<string, object?>
            {
                ["message_id"] = replyToMessageId,
                ["allow_sending_without_reply"] = true
            };
        }

        using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(url, content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Telegram publish failed ({(int)response.StatusCode}): {responseBody}");
        }

        _logger.LogInformation("Published Telegram message for {Speaker}", message.ProducedBy);
    }

    private static string FormatOutgoingText(RoomMessage message)
    {
        return message.ProducedBy switch
        {
            AgentProfile.Aristotle or AgentProfile.Freud or AgentProfile.Marcus => message.Text.Trim(),
            AgentProfile.Moderator => $"Moderator: {message.Text.Trim()}",
            _ => message.Text.Trim()
        };
    }
}
