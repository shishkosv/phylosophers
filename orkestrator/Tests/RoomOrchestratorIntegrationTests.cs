using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orkestrator.Config;
using Orkestrator.Models;
using Orkestrator.Orchestration;
using Orkestrator.Services;
using Xunit;

namespace Orkestrator.Tests;

public sealed class RoomOrchestratorIntegrationTests
{
    [Fact]
    public async Task HandleIncomingMessageAsync_PublishesOneReply_AndPersistsLastSpeaker()
    {
        var stateStore = new InMemoryStateStore();
        var publisher = new RecordingTelegramPublisher();
        var agentInvoker = new DeterministicInvoker();
        var options = Options.Create(new OrchestratorOptions
        {
            MaxPhilosopherRepliesPerTurn = 2,
            ModeratorSummaryWords = 90,
            HistoryLimit = 50,
            SimilarityThreshold = 0.99
        });

        var orchestrator = new RoomOrchestrator(
            new ContrastPolicy(),
            agentInvoker,
            NullLogger<RoomOrchestrator>.Instance,
            new ModeratorSelector(agentInvoker),
            options,
            new RepetitionGuard(options),
            stateStore,
            publisher);

        var userMessage = new RoomMessage
        {
            Id = "42",
            SenderId = "user",
            SenderName = "TelegramUser",
            Text = "I feel shame and contradict myself",
            TimestampUtc = DateTimeOffset.UtcNow,
            IsHuman = true
        };

        var result = await orchestrator.HandleIncomingMessageAsync(userMessage);
        var state = await stateStore.LoadAsync();

        Assert.False(result.StayedSilent);
        Assert.Single(result.PublishedMessages);
        Assert.Single(publisher.Messages);
        Assert.Equal(AgentProfile.Freud, publisher.Messages[0].ProducedBy);
        Assert.Equal(AgentProfile.Freud, state.LastSpeaker);
    }

    private sealed class DeterministicInvoker : IAgentInvoker
    {
        public Task<string?> InvokeAsync(AgentProfile profile, IReadOnlyList<RoomMessage> history, RoomMessage userMessage, int maxWords, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>($"Reply from {profile}");

        public Task<RouteDecision> InvokeModeratorAsync(IReadOnlyList<RoomMessage> history, RoomMessage userMessage, CancellationToken cancellationToken = default)
            => Task.FromResult(new RouteDecision
            {
                Action = RouteAction.SelectSpeaker,
                Speaker = AgentProfile.Freud,
                Reason = "Test route",
                MaxWords = 80,
                RequiresContrastSpeaker = false
            });
    }

    private sealed class RecordingTelegramPublisher : ITelegramPublisher
    {
        public List<RoomMessage> Messages { get; } = [];

        public Task PublishAsync(RoomMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryStateStore : IRoomStateStore
    {
        private RoomState _state = new();

        public Task<RoomState> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_state);

        public Task SaveAsync(RoomState state, CancellationToken cancellationToken = default)
        {
            _state = state;
            return Task.CompletedTask;
        }
    }
}
