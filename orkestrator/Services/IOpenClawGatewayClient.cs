namespace Orkestrator.Services;

public interface IOpenClawGatewayClient
{
    Task<string?> SendAsync(string sessionKey, string prompt, CancellationToken cancellationToken = default);
}
