using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orkestrator.Models;

namespace Orkestrator.Services;

public static class OpenClawBridgeApi
{
    public static IServiceCollection AddOpenClawBridgeApi(this IServiceCollection services)
    {
        services.AddSingleton<OpenClawBridgeWorker>();
        return services;
    }

    public static WebApplication MapOpenClawBridgeApi(this WebApplication app, string routePath)
    {
        app.MapPost(routePath, async (OpenClawBridgeRequest request, OpenClawBridgeWorker worker, ILogger<OpenClawBridgeWorker> logger, CancellationToken cancellationToken) =>
        {
            var response = await worker.ProcessAsync(request, cancellationToken);
            if (!response.Ok)
            {
                logger.LogWarning("Bridge request failed. Kind={Kind}, Profile={Profile}, SessionKeyPresent={SessionKeyPresent}, Error={Error}", request.Kind, request.Profile, !string.IsNullOrWhiteSpace(request.SessionKey), response.Error);
            }

            return response.Ok
                ? Results.Json(response)
                : Results.Json(response, statusCode: StatusCodes.Status400BadRequest);
        });

        app.MapGet("/health", () => Results.Json(new { ok = true }));
        return app;
    }
}
