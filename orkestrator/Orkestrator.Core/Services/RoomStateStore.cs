using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orkestrator.Config;
using Orkestrator.Models;

namespace Orkestrator.Services;

public sealed class RoomStateStore : IRoomStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ILogger<RoomStateStore> _logger;
    private readonly string _stateFilePath;

    public RoomStateStore(IOptions<OrchestratorOptions> options, ILogger<RoomStateStore> logger)
    {
        _logger = logger;
        _stateFilePath = Path.GetFullPath(options.Value.StateFilePath, AppContext.BaseDirectory);
    }

    public async Task<RoomState> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_stateFilePath))
            {
                _logger.LogInformation("Room state file {Path} does not exist, using empty state", _stateFilePath);
                return new RoomState();
            }

            _logger.LogDebug("Loading room state from {Path}", _stateFilePath);
            await using var stream = File.OpenRead(_stateFilePath);
            var state = await JsonSerializer.DeserializeAsync<RoomState>(stream, JsonOptions, cancellationToken);

            if (state is null)
            {
                _logger.LogWarning("Room state file {Path} was empty or invalid, using empty state", _stateFilePath);
                return new RoomState();
            }

            _logger.LogDebug("Loaded room state from {Path} with {HistoryCount} messages, last speaker {LastSpeaker}, last turn {LastTurnId}", _stateFilePath, state.History.Count, state.LastSpeaker, state.LastTurnId);
            return state;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Loading room state from {Path} was canceled", _stateFilePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load room state from {Path}", _stateFilePath);
            throw;
        }
    }

    public async Task SaveAsync(RoomState state, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_stateFilePath)!);
            state.UpdatedAtUtc = DateTimeOffset.UtcNow;

            _logger.LogDebug("Saving room state to {Path} with {HistoryCount} messages, last speaker {LastSpeaker}, last turn {LastTurnId}", _stateFilePath, state.History.Count, state.LastSpeaker, state.LastTurnId);
            await using var stream = File.Create(_stateFilePath);
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken);
            _logger.LogDebug("Saved room state to {Path}", _stateFilePath);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Saving room state to {Path} was canceled", _stateFilePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save room state to {Path}", _stateFilePath);
            throw;
        }
    }
}
