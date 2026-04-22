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
    public async Task HandleIncomingMessageAsync_PublishesSingleReply_AndPersistsTurnState()
    {
        var harness = CreateHarness();
        harness.Invoker.ModeratorDecision = new RouteDecision
        {
            Action = RouteAction.SelectSpeaker,
            Speaker = AgentProfile.Freud,
            Reason = "Test route",
            MaxWords = 80
        };
        harness.Invoker.Replies[AgentProfile.Freud] = "A focused Freudian reply about shame and contradiction.";

        var userMessage = CreateUserMessage("42", "I feel shame and contradict myself");

        var result = await harness.Orchestrator.HandleIncomingMessageAsync(userMessage);
        var state = await harness.StateStore.LoadAsync();

        Assert.False(result.StayedSilent);
        Assert.False(result.PublishedSummary);
        Assert.Single(result.PublishedMessages);
        Assert.Single(harness.Publisher.Messages);
        Assert.Equal(AgentProfile.Freud, result.PublishedMessages[0].ProducedBy);
        Assert.Equal(AgentProfile.Freud, state.LastSpeaker);
        Assert.Equal(userMessage.Id, state.LastTurnId);
        Assert.Contains(state.History, m => m.Id == userMessage.Id);
        Assert.Contains(state.History, m => m.ProducedBy == AgentProfile.Freud);
    }

    [Fact]
    public async Task HandleIncomingMessageAsync_SilenceDecision_PersistsLastTurnWithoutPublishing()
    {
        var harness = CreateHarness();
        harness.Invoker.ModeratorDecision = new RouteDecision
        {
            Action = RouteAction.Silence,
            Speaker = AgentProfile.None,
            Reason = "Stay quiet"
        };

        var userMessage = CreateUserMessage("43", "Just passing through");

        var result = await harness.Orchestrator.HandleIncomingMessageAsync(userMessage);
        var state = await harness.StateStore.LoadAsync();

        Assert.True(result.StayedSilent);
        Assert.False(result.PublishedSummary);
        Assert.Empty(result.PublishedMessages);
        Assert.Empty(harness.Publisher.Messages);
        Assert.Equal(userMessage.Id, state.LastTurnId);
        Assert.Equal(AgentProfile.None, state.LastSpeaker);
        Assert.Contains(state.History, m => m.Id == userMessage.Id);
    }

    [Fact]
    public async Task HandleIncomingMessageAsync_SameSpeakerSuppressed_PersistsLastTurn()
    {
        var harness = CreateHarness(new RoomState
        {
            LastSpeaker = AgentProfile.Freud,
            History =
            {
                CreateAgentMessage(AgentProfile.Freud, "Earlier Freud reply")
            }
        });

        harness.Invoker.ModeratorDecision = new RouteDecision
        {
            Action = RouteAction.SelectSpeaker,
            Speaker = AgentProfile.Freud,
            Reason = "Would repeat speaker",
            MaxWords = 80
        };
        harness.Invoker.Replies[AgentProfile.Freud] = "This should never publish.";

        var userMessage = CreateUserMessage("44", "I still feel ashamed");

        var result = await harness.Orchestrator.HandleIncomingMessageAsync(userMessage);
        var state = await harness.StateStore.LoadAsync();

        Assert.True(result.StayedSilent);
        Assert.Empty(harness.Publisher.Messages);
        Assert.Equal(AgentProfile.Freud, state.LastSpeaker);
        Assert.Equal(userMessage.Id, state.LastTurnId);
    }

    [Fact]
    public async Task HandleIncomingMessageAsync_PublishesContrastAndModeratorSummary()
    {
        var harness = CreateHarness();
        harness.Invoker.ModeratorDecision = new RouteDecision
        {
            Action = RouteAction.SelectSpeaker,
            Speaker = AgentProfile.Freud,
            Reason = "Contrast-worthy route",
            MaxWords = 90
        };
        harness.Invoker.Replies[AgentProfile.Freud] = LongReply("Freud", "shame contradiction hidden motives conflict repression");
        harness.Invoker.Replies[AgentProfile.Aristotle] = "Aristotle answers by distinguishing habit, excess, deficiency, and the mean in action.";
        harness.Invoker.Replies[AgentProfile.Moderator] = "Moderator summary drawing together both perspectives.";

        var userMessage = CreateUserMessage("45", "I feel shame and contradict myself in ways that affect my habits and ethics");

        var result = await harness.Orchestrator.HandleIncomingMessageAsync(userMessage);
        var state = await harness.StateStore.LoadAsync();

        Assert.False(result.StayedSilent);
        Assert.True(result.PublishedSummary);
        Assert.Equal(3, result.PublishedMessages.Count);
        Assert.Collection(
            harness.Publisher.Messages,
            first => Assert.Equal(AgentProfile.Freud, first.ProducedBy),
            second => Assert.Equal(AgentProfile.Aristotle, second.ProducedBy),
            third => Assert.Equal(AgentProfile.Moderator, third.ProducedBy));
        Assert.Equal(AgentProfile.Aristotle, state.LastSpeaker);
        Assert.Equal(userMessage.Id, state.LastTurnId);
        Assert.Equal(1, harness.Invoker.ModeratorInvokeCount);
        Assert.Equal(3, harness.Invoker.InvokeOrder.Count);
        Assert.Equal(AgentProfile.Freud, harness.Invoker.InvokeOrder[0]);
        Assert.Equal(AgentProfile.Aristotle, harness.Invoker.InvokeOrder[1]);
        Assert.Equal(AgentProfile.Moderator, harness.Invoker.InvokeOrder[2]);
    }

    [Fact]
    public async Task HandleIncomingMessageAsync_RepetitionSuppression_SkipsDuplicatePublish()
    {
        var repeatedText = "Shared repeated insight about shame conflict hidden motives and habits.";
        var harness = CreateHarness(new RoomState
        {
            History =
            {
                CreateAgentMessage(AgentProfile.Freud, repeatedText)
            }
        }, similarityThreshold: 0.5);

        harness.Invoker.ModeratorDecision = new RouteDecision
        {
            Action = RouteAction.SelectSpeaker,
            Speaker = AgentProfile.Freud,
            Reason = "Would duplicate",
            MaxWords = 80
        };
        harness.Invoker.Replies[AgentProfile.Freud] = repeatedText;

        var userMessage = CreateUserMessage("46", "I repeat the same conflict again");

        var result = await harness.Orchestrator.HandleIncomingMessageAsync(userMessage);
        var state = await harness.StateStore.LoadAsync();

        Assert.True(result.StayedSilent);
        Assert.Empty(harness.Publisher.Messages);
        Assert.False(result.PublishedSummary);
        Assert.Equal(userMessage.Id, state.LastTurnId);
        Assert.Equal(AgentProfile.None, state.LastSpeaker);
    }

    [Fact]
    public async Task HandleIncomingMessageAsync_PublishFailure_LogsAndPropagates()
    {
        var harness = CreateHarness();
        harness.Invoker.ModeratorDecision = new RouteDecision
        {
            Action = RouteAction.SelectSpeaker,
            Speaker = AgentProfile.Freud,
            Reason = "Test failure propagation",
            MaxWords = 80
        };
        harness.Invoker.Replies[AgentProfile.Freud] = "A publishable Freudian reply.";
        harness.Publisher.ExceptionToThrow = new InvalidOperationException("Telegram is down");

        var userMessage = CreateUserMessage("47", "Why do I sabotage myself?");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Orchestrator.HandleIncomingMessageAsync(userMessage));
        var state = await harness.StateStore.LoadAsync();

        Assert.Equal("Telegram is down", ex.Message);
        Assert.Null(state.LastTurnId);
        Assert.Empty(harness.Publisher.Messages);
    }

    [Fact]
    public async Task HandleIncomingMessageAsync_ContrastSuppressed_WhenTooSimilarToHistory()
    {
        var repeatedContrast = "Aristotle answers by distinguishing habit, excess, deficiency, and the mean in action.";
        var harness = CreateHarness(new RoomState
        {
            History =
            {
                CreateAgentMessage(AgentProfile.Aristotle, repeatedContrast)
            }
        }, similarityThreshold: 0.5);

        harness.Invoker.ModeratorDecision = new RouteDecision
        {
            Action = RouteAction.SelectSpeaker,
            Speaker = AgentProfile.Freud,
            Reason = "Contrast route",
            MaxWords = 90
        };
        harness.Invoker.Replies[AgentProfile.Freud] = LongReply("Freud", "shame contradiction hidden motives conflict repression habit ethic");
        harness.Invoker.Replies[AgentProfile.Aristotle] = repeatedContrast;
        harness.Invoker.Replies[AgentProfile.Moderator] = "This summary should not be emitted.";

        var result = await harness.Orchestrator.HandleIncomingMessageAsync(CreateUserMessage("48", "My shame and habits are in conflict with my ethics"));
        var state = await harness.StateStore.LoadAsync();

        Assert.False(result.StayedSilent);
        Assert.False(result.PublishedSummary);
        Assert.Single(result.PublishedMessages);
        Assert.Single(harness.Publisher.Messages);
        Assert.Equal(AgentProfile.Freud, harness.Publisher.Messages[0].ProducedBy);
        Assert.Equal(AgentProfile.Freud, state.LastSpeaker);
        Assert.DoesNotContain(harness.Publisher.Messages, m => m.ProducedBy == AgentProfile.Moderator);
    }

    [Fact]
    public async Task HandleIncomingMessageAsync_TrimHistory_RespectsHistoryLimit()
    {
        var initialState = new RoomState();
        for (var i = 0; i < 5; i++)
        {
            initialState.History.Add(CreateUserMessage($"seed-{i}", $"seed message {i}"));
        }

        var harness = CreateHarness(initialState, historyLimit: 3);
        harness.Invoker.ModeratorDecision = new RouteDecision
        {
            Action = RouteAction.Silence,
            Speaker = AgentProfile.None,
            Reason = "trim only"
        };

        var result = await harness.Orchestrator.HandleIncomingMessageAsync(CreateUserMessage("49", "latest message"));
        var state = await harness.StateStore.LoadAsync();

        Assert.True(result.StayedSilent);
        Assert.Equal(3, state.History.Count);
        Assert.Equal("seed-3", state.History[0].Id);
        Assert.Equal("seed-4", state.History[1].Id);
        Assert.Equal("49", state.History[2].Id);
    }

    private static TestHarness CreateHarness(RoomState? initialState = null, double similarityThreshold = 0.99, int historyLimit = 50)
    {
        var stateStore = new InMemoryStateStore(initialState ?? new RoomState());
        var publisher = new RecordingTelegramPublisher();
        var invoker = new DeterministicInvoker();
        var options = Options.Create(new OrchestratorOptions
        {
            MaxPhilosopherRepliesPerTurn = 2,
            ModeratorSummaryWords = 90,
            HistoryLimit = historyLimit,
            SimilarityThreshold = similarityThreshold
        });

        var orchestrator = new RoomOrchestrator(
            new ContrastPolicy(),
            invoker,
            NullLogger<RoomOrchestrator>.Instance,
            new ModeratorSelector(invoker),
            options,
            new RepetitionGuard(options),
            stateStore,
            publisher);

        return new TestHarness(orchestrator, invoker, stateStore, publisher);
    }

    private static RoomMessage CreateUserMessage(string id, string text)
        => new()
        {
            Id = id,
            SenderId = "user",
            SenderName = "TelegramUser",
            Text = text,
            TimestampUtc = DateTimeOffset.UtcNow,
            IsHuman = true
        };

    private static RoomMessage CreateAgentMessage(AgentProfile profile, string text)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            SenderId = profile.ToString().ToLowerInvariant(),
            SenderName = profile.ToString(),
            Text = text,
            TimestampUtc = DateTimeOffset.UtcNow,
            IsHuman = false,
            ProducedBy = profile
        };

    private static string LongReply(string speaker, string topicTerms)
        => $"{speaker} offers a long reflection on {topicTerms}. " +
           "This reply is deliberately extended so that it exceeds the contrast threshold and invites a second perspective. " +
           "It keeps elaborating on the same issue with enough length to trigger contrast while remaining distinct in wording.";

    private sealed record TestHarness(
        RoomOrchestrator Orchestrator,
        DeterministicInvoker Invoker,
        InMemoryStateStore StateStore,
        RecordingTelegramPublisher Publisher);

    private sealed class DeterministicInvoker : IAgentInvoker
    {
        public RouteDecision ModeratorDecision { get; set; } = new();
        public Dictionary<AgentProfile, string?> Replies { get; } = new();
        public List<AgentProfile> InvokeOrder { get; } = [];
        public int ModeratorInvokeCount { get; private set; }

        public Task<string?> InvokeAsync(AgentProfile profile, IReadOnlyList<RoomMessage> history, RoomMessage userMessage, int maxWords, CancellationToken cancellationToken = default)
        {
            InvokeOrder.Add(profile);
            Replies.TryGetValue(profile, out var reply);
            return Task.FromResult(reply);
        }

        public Task<RouteDecision> InvokeModeratorAsync(IReadOnlyList<RoomMessage> history, RoomMessage userMessage, CancellationToken cancellationToken = default)
        {
            ModeratorInvokeCount++;
            return Task.FromResult(ModeratorDecision);
        }
    }

    private sealed class RecordingTelegramPublisher : ITelegramPublisher
    {
        public List<RoomMessage> Messages { get; } = [];
        public Exception? ExceptionToThrow { get; set; }

        public Task PublishAsync(RoomMessage message, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryStateStore : IRoomStateStore
    {
        private RoomState _state;

        public InMemoryStateStore(RoomState initialState)
        {
            _state = initialState;
        }

        public Task<RoomState> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_state);

        public Task SaveAsync(RoomState state, CancellationToken cancellationToken = default)
        {
            _state = state;
            return Task.CompletedTask;
        }
    }
}
