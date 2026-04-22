using Microsoft.Extensions.Logging;
using Orkestrator.Models;

namespace Orkestrator.Services;

public sealed class TelegramPublisher : ITelegramPublisher
{
    private readonly ILogger<TelegramPublisher> _logger;

    public TelegramPublisher(ILogger<TelegramPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(RoomMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Publish placeholder -> [{Speaker}] {Text}", message.ProducedBy, message.Text);
        return Task.CompletedTask;
    }
}
