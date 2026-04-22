using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orkestrator.Config;
using Orkestrator.Models;

namespace Orkestrator.Services;

public sealed class TelegramPollingStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ILogger<TelegramPollingStateStore> _logger;
    private readonly string _stateFilePath;

    public TelegramPollingStateStore(IOptions<OrchestratorOptions> options, ILogger<TelegramPollingStateStore> logger)
    {
        _logger = logger;
        _stateFilePath = Path.GetFullPath(options.Value.Telegram.PollingOffsetStateFilePath, AppContext.BaseDirectory);
    }

    public async Task<TelegramPollingState> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_stateFilePath))
            {
                _logger.LogInformation("Telegram polling state file {Path} does not exist, starting from empty offset", _stateFilePath);
                return new TelegramPollingState();
            }

            await using var stream = File.OpenRead(_stateFilePath);
            var state = await JsonSerializer.DeserializeAsync<TelegramPollingState>(stream, JsonOptions, cancellationToken);
            return state ?? new TelegramPollingState();
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Loading Telegram polling state from {Path} was canceled", _stateFilePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Telegram polling state from {Path}", _stateFilePath);
            throw;
        }
    }

    public async Task SaveAsync(TelegramPollingState state, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_stateFilePath)!);
            state.UpdatedAtUtc = DateTimeOffset.UtcNow;

            await using var stream = File.Create(_stateFilePath);
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken);
            _logger.LogDebug("Saved Telegram polling state to {Path} with lastUpdateId={LastUpdateId}", _stateFilePath, state.LastUpdateId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Saving Telegram polling state to {Path} was canceled", _stateFilePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Telegram polling state to {Path}", _stateFilePath);
            throw;
        }
    }
}
