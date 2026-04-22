using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orkestrator.Config;
using Orkestrator.Models;
using Orkestrator.Services;

namespace Orkestrator.Orchestration;

public sealed class TelegramLongPollingService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger<TelegramLongPollingService> _logger;
    private readonly OrchestratorOptions _options;
    private readonly HttpClient _httpClient;
    private readonly RoomOrchestrator _orchestrator;
    private readonly TelegramPollingStateStore _stateStore;

    public TelegramLongPollingService(
        HttpClient httpClient,
        IOptions<OrchestratorOptions> options,
        RoomOrchestrator orchestrator,
        TelegramPollingStateStore stateStore,
        ILogger<TelegramLongPollingService> logger)
    {
        _httpClient = httpClient;
        _orchestrator = orchestrator;
        _stateStore = stateStore;
        _logger = logger;
        _options = options.Value;
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(_options.Telegram.PollingTimeoutSeconds + 10, 15));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telegram polling service starting. Enabled={Enabled}, chatIdConfigured={ChatIdConfigured}, botTokenConfigured={BotTokenConfigured}, sessionKeyConfigured={SessionKeyConfigured}, pollingTimeoutSeconds={PollingTimeoutSeconds}, pollingLimit={PollingLimit}",
            _options.Telegram.EnablePolling,
            !string.IsNullOrWhiteSpace(_options.Telegram.ChatId),
            !string.IsNullOrWhiteSpace(_options.Telegram.BotToken),
            !string.IsNullOrWhiteSpace(_options.OpenClaw.SessionKey),
            _options.Telegram.PollingTimeoutSeconds,
            _options.Telegram.PollingLimit);

        if (!_options.Telegram.EnablePolling)
        {
            _logger.LogInformation("Telegram polling is disabled");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.Telegram.BotToken) || string.IsNullOrWhiteSpace(_options.Telegram.ChatId))
        {
            _logger.LogWarning("Telegram polling cannot start because BotToken or ChatId is not configured");
            return;
        }

        var state = await _stateStore.LoadAsync(stoppingToken);
        _logger.LogInformation("Telegram polling started with lastUpdateId={LastUpdateId} for chatId={ChatId}", state.LastUpdateId, _options.Telegram.ChatId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var updates = await GetUpdatesAsync(state.LastUpdateId, stoppingToken);
                if (updates.Count == 0)
                {
                    continue;
                }

                foreach (var update in updates.OrderBy(x => x.UpdateId))
                {
                    state.LastUpdateId = update.UpdateId;
                    await _stateStore.SaveAsync(state, stoppingToken);

                    if (!TryMapToRoomMessage(update, out var roomMessage))
                    {
                        _logger.LogDebug("Skipping Telegram update {UpdateId} because it has no supported text message", update.UpdateId);
                        continue;
                    }

                    _logger.LogInformation("Processing Telegram update {UpdateId}, messageId={MessageId}, sender={SenderName}", update.UpdateId, roomMessage.Id, roomMessage.SenderName);
                    await _orchestrator.HandleIncomingMessageAsync(roomMessage, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telegram polling loop failed; retrying in 5 seconds");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Telegram polling stopped");
    }

    private async Task<List<TelegramUpdate>> GetUpdatesAsync(long? lastUpdateId, CancellationToken cancellationToken)
    {
        var offset = lastUpdateId.HasValue ? lastUpdateId.Value + 1 : (long?)null;
        var url = $"https://api.telegram.org/bot{_options.Telegram.BotToken}/getUpdates";
        var payload = new Dictionary<string, object?>
        {
            ["timeout"] = _options.Telegram.PollingTimeoutSeconds,
            ["limit"] = _options.Telegram.PollingLimit,
            ["allowed_updates"] = new[] { "message" }
        };

        if (offset.HasValue)
        {
            payload["offset"] = offset.Value;
        }

        using var response = await _httpClient.PostAsJsonAsync(url, payload, JsonOptions, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Telegram getUpdates failed with statusCode={StatusCode}, offset={Offset}, body={Body}", (int)response.StatusCode, offset, body);
            throw new InvalidOperationException($"Telegram getUpdates failed ({(int)response.StatusCode}): {body}");
        }

        var parsed = JsonSerializer.Deserialize<TelegramGetUpdatesResponse>(body, JsonOptions);
        if (parsed is null || !parsed.Ok)
        {
            _logger.LogError("Telegram getUpdates returned unexpected payload for offset={Offset}: {Body}", offset, body);
            throw new InvalidOperationException($"Telegram getUpdates returned unexpected payload: {body}");
        }

        _logger.LogDebug("Fetched {Count} Telegram updates starting from offset {Offset}", parsed.Result.Count, offset);
        return parsed.Result;
    }

    private bool TryMapToRoomMessage(TelegramUpdate update, out RoomMessage roomMessage)
    {
        roomMessage = null!;

        var message = update.Message;
        if (message is null || string.IsNullOrWhiteSpace(message.Text) || message.From is null || message.Chat is null)
        {
            return false;
        }

        if (message.From.IsBot)
        {
            _logger.LogDebug("Skipping bot-authored Telegram message {MessageId}", message.MessageId);
            return false;
        }

        if (_options.Telegram.ChatId != message.Chat.Id.ToString())
        {
            _logger.LogDebug("Skipping Telegram message {MessageId} from unexpected chat {ChatId}", message.MessageId, message.Chat.Id);
            return false;
        }

        roomMessage = new RoomMessage
        {
            Id = message.MessageId.ToString(),
            SenderId = message.From.Id.ToString(),
            SenderName = FormatSenderName(message.From),
            Text = message.Text.Trim(),
            TimestampUtc = DateTimeOffset.FromUnixTimeSeconds(message.DateUnix),
            IsHuman = true,
            ReplyToMessageId = message.ReplyToMessage?.MessageId.ToString()
        };

        return true;
    }

    private static string FormatSenderName(TelegramUser user)
    {
        var fullName = string.Join(' ', new[] { user.FirstName, user.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        return !string.IsNullOrWhiteSpace(fullName)
            ? fullName
            : !string.IsNullOrWhiteSpace(user.Username)
                ? user.Username!
                : user.Id.ToString();
    }
}
