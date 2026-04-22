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
        if (!File.Exists(_stateFilePath))
        {
            return new RoomState();
        }

        await using var stream = File.OpenRead(_stateFilePath);
        var state = await JsonSerializer.DeserializeAsync<RoomState>(stream, JsonOptions, cancellationToken);
        return state ?? new RoomState();
    }

    public async Task SaveAsync(RoomState state, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_stateFilePath)!);
        state.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await using var stream = File.Create(_stateFilePath);
        await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken);
        _logger.LogDebug("Saved room state to {Path}", _stateFilePath);
    }
}
