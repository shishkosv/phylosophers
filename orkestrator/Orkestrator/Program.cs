using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orkestrator.Config;
using Orkestrator.Models;
using Orkestrator.Orchestration;
using Orkestrator.Services;

if (args.Length > 0 && string.Equals(args[0], "serve", StringComparison.OrdinalIgnoreCase))
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Configuration.SetBasePath(AppContext.BaseDirectory);
    builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    builder.Configuration.AddEnvironmentVariables(prefix: "ORKESTRATOR_");

    builder.Services.Configure<OrchestratorOptions>(builder.Configuration.GetSection("Orchestrator"));
    builder.Services.AddHttpClient<IOpenClawGatewayClient, OpenClawGatewayHttpClient>();
    builder.Services.AddHttpClient<OpenClawHttpBridge>();
    builder.Services.AddSingleton<IOpenClawBridge, OpenClawLocalBridge>();
    builder.Services.AddSingleton<IAgentInvoker, OpenClawAgentInvoker>();
    builder.Services.AddHttpClient<ITelegramPublisher, TelegramPublisher>();
    builder.Services.AddHttpClient<TelegramLongPollingService>();
    builder.Services.AddSingleton<TelegramPollingStateStore>();
    builder.Services.AddSingleton<IRoomStateStore, RoomStateStore>();
    builder.Services.AddSingleton<ContrastPolicy>();
    builder.Services.AddSingleton<RepetitionGuard>();
    builder.Services.AddSingleton<ModeratorSelector>();
    builder.Services.AddSingleton<RoomOrchestrator>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<TelegramLongPollingService>());
    builder.Services.AddOpenClawBridgeApi();

    var app = builder.Build();
    var options = app.Services.GetRequiredService<IOptions<OrchestratorOptions>>().Value;
    app.MapOpenClawBridgeApi(options.OpenClaw.InternalBridge.RoutePath);
    await app.RunAsync(options.OpenClaw.InternalBridge.Url);
    return;
}

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.SetBasePath(AppContext.BaseDirectory);
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        config.AddEnvironmentVariables(prefix: "ORKESTRATOR_");
    })
    .ConfigureServices((context, services) =>
    {
        services.Configure<OrchestratorOptions>(context.Configuration.GetSection("Orchestrator"));
        services.AddHttpClient<OpenClawHttpBridge>();
        services.AddSingleton<IOpenClawBridge, OpenClawLocalBridge>();
        services.AddSingleton<IAgentInvoker, OpenClawAgentInvoker>();
        services.AddHttpClient<ITelegramPublisher, TelegramPublisher>();
        services.AddHttpClient<TelegramLongPollingService>();
        services.AddSingleton<TelegramPollingStateStore>();
        services.AddSingleton<IRoomStateStore, RoomStateStore>();
        services.AddSingleton<ContrastPolicy>();
        services.AddSingleton<RepetitionGuard>();
        services.AddSingleton<ModeratorSelector>();
        services.AddSingleton<RoomOrchestrator>();
        services.AddHostedService(sp => sp.GetRequiredService<TelegramLongPollingService>());
    })
    .Build();

if (args.Length == 0)
{
    Console.WriteLine("Provide a user message as the first argument, or run with 'serve' to start the internal bridge API.");
    return;
}

var orchestrator = host.Services.GetRequiredService<RoomOrchestrator>();
var message = new RoomMessage
{
    Id = Guid.NewGuid().ToString("N"),
    SenderId = "user",
    SenderName = "TelegramUser",
    Text = string.Join(' ', args),
    TimestampUtc = DateTimeOffset.UtcNow,
    IsHuman = true
};

var result = await orchestrator.HandleIncomingMessageAsync(message);
Console.WriteLine($"Published messages: {result.PublishedMessages.Count}, silent: {result.StayedSilent}, summary: {result.PublishedSummary}");
