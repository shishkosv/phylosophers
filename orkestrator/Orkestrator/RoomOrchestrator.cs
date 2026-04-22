using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orkestrator.Config;
using Orkestrator.Models;
using Orkestrator.Services;

namespace Orkestrator.Orchestration;

public sealed class RoomOrchestrator
{
    private readonly ContrastPolicy _contrastPolicy;
    private readonly IAgentInvoker _agentInvoker;
    private readonly ILogger<RoomOrchestrator> _logger;
    private readonly ModeratorSelector _moderatorSelector;
    private readonly OrchestratorOptions _options;
    private readonly RepetitionGuard _repetitionGuard;
    private readonly IRoomStateStore _stateStore;
    private readonly ITelegramPublisher _telegramPublisher;

    public RoomOrchestrator(
        ContrastPolicy contrastPolicy,
        IAgentInvoker agentInvoker,
        ILogger<RoomOrchestrator> logger,
        ModeratorSelector moderatorSelector,
        IOptions<OrchestratorOptions> options,
        RepetitionGuard repetitionGuard,
        IRoomStateStore stateStore,
        ITelegramPublisher telegramPublisher)
    {
        _contrastPolicy = contrastPolicy;
        _agentInvoker = agentInvoker;
        _logger = logger;
        _moderatorSelector = moderatorSelector;
        _options = options.Value;
        _repetitionGuard = repetitionGuard;
        _stateStore = stateStore;
        _telegramPublisher = telegramPublisher;
    }

    public async Task<OrchestrationTurnResult> HandleIncomingMessageAsync(RoomMessage userMessage, CancellationToken cancellationToken = default)
    {
        var state = await _stateStore.LoadAsync(cancellationToken);
        state.History.Add(userMessage);
        TrimHistory(state);

        var published = new List<RoomMessage>();
        var decision = await _moderatorSelector.SelectAsync(state.History, userMessage, cancellationToken);
        if (decision.Action is RouteAction.Silence or RouteAction.Close || decision.Speaker == AgentProfile.None)
        {
            await _stateStore.SaveAsync(state, cancellationToken);
            return new OrchestrationTurnResult { StayedSilent = true };
        }

        if (!RoomRules.CanSpeak(decision.Speaker, state.LastSpeaker))
        {
            _logger.LogInformation("Suppressing speaker {Speaker} because they spoke last turn", decision.Speaker);
            await _stateStore.SaveAsync(state, cancellationToken);
            return new OrchestrationTurnResult { StayedSilent = true };
        }

        var firstReply = await _agentInvoker.InvokeAsync(decision.Speaker, state.History, userMessage, decision.MaxWords, cancellationToken);
        if (!string.IsNullOrWhiteSpace(firstReply) && !_repetitionGuard.IsTooSimilar(firstReply, state.History))
        {
            var firstMessage = CreateAgentMessage(decision.Speaker, firstReply, userMessage.Id);
            await _telegramPublisher.PublishAsync(firstMessage, cancellationToken);
            state.History.Add(firstMessage);
            state.LastSpeaker = decision.Speaker;
            published.Add(firstMessage);
        }

        if (published.Count < _options.MaxPhilosopherRepliesPerTurn && !string.IsNullOrWhiteSpace(firstReply))
        {
            var contrastSpeaker = RoomRules.GetContrastSpeaker(decision.Speaker);
            if (_contrastPolicy.ShouldAddContrast(decision.Speaker, firstReply, userMessage, state.LastSpeaker))
            {
                var secondReply = await _agentInvoker.InvokeAsync(contrastSpeaker, state.History, userMessage, decision.MaxWords, cancellationToken);
                if (!string.IsNullOrWhiteSpace(secondReply) && !_repetitionGuard.IsTooSimilar(secondReply, state.History))
                {
                    var secondMessage = CreateAgentMessage(contrastSpeaker, secondReply, userMessage.Id);
                    await _telegramPublisher.PublishAsync(secondMessage, cancellationToken);
                    state.History.Add(secondMessage);
                    state.LastSpeaker = contrastSpeaker;
                    published.Add(secondMessage);
                }
            }
        }

        var shouldSummarize = published.Count >= 2;
        if (shouldSummarize)
        {
            var summary = await _agentInvoker.InvokeAsync(AgentProfile.Moderator, state.History, userMessage, _options.ModeratorSummaryWords, cancellationToken);
            if (!string.IsNullOrWhiteSpace(summary) && !_repetitionGuard.IsTooSimilar(summary, state.History))
            {
                var summaryMessage = CreateAgentMessage(AgentProfile.Moderator, summary, userMessage.Id);
                await _telegramPublisher.PublishAsync(summaryMessage, cancellationToken);
                state.History.Add(summaryMessage);
                published.Add(summaryMessage);
            }
        }

        await _stateStore.SaveAsync(state, cancellationToken);
        return new OrchestrationTurnResult
        {
            PublishedMessages = published,
            PublishedSummary = shouldSummarize,
            StayedSilent = published.Count == 0
        };
    }

    private void TrimHistory(RoomState state)
    {
        if (state.History.Count <= _options.HistoryLimit)
        {
            return;
        }

        var overflow = state.History.Count - _options.HistoryLimit;
        state.History.RemoveRange(0, overflow);
    }

    private static RoomMessage CreateAgentMessage(AgentProfile profile, string text, string replyToMessageId)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            SenderId = profile.ToString().ToLowerInvariant(),
            SenderName = profile.ToString(),
            Text = text.Trim(),
            TimestampUtc = DateTimeOffset.UtcNow,
            IsHuman = false,
            ProducedBy = profile,
            ReplyToMessageId = replyToMessageId
        };
}
