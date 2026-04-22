using Orkestrator.Models;

namespace Orkestrator.Services;

public interface IRoomStateStore
{
    Task<RoomState> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(RoomState state, CancellationToken cancellationToken = default);
}
