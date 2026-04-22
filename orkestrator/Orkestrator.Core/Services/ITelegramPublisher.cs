using Orkestrator.Models;

namespace Orkestrator.Services;

public interface ITelegramPublisher
{
    Task PublishAsync(RoomMessage message, CancellationToken cancellationToken = default);
}
