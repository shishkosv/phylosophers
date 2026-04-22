using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orkestrator.Config;
using Orkestrator.Models;
using Orkestrator.Orchestration;
using Orkestrator.Services;

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
        services.AddHttpClient<IAgentInvoker, OpenClawAgentInvoker>();
        services.AddHttpClient<ITelegramPublisher, TelegramPublisher>();
        services.AddSingleton<IRoomStateStore, RoomStateStore>();
        services.AddSingleton<ContrastPolicy>();
        services.AddSingleton<RepetitionGuard>();
        services.AddSingleton<ModeratorSelector>();
        services.AddSingleton<RoomOrchestrator>();
    })
    .Build();

if (args.Length == 0)
{
    Console.WriteLine("Provide a user message as the first argument.");
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
