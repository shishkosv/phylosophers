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
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["TurnMessageId"] = userMessage.Id,
            ["TurnSenderId"] = userMessage.SenderId,
            ["TurnSenderName"] = userMessage.SenderName,
            ["TurnReplyToMessageId"] = userMessage.ReplyToMessageId
        });

        try
        {
            _logger.LogInformation("Handling incoming message from {Sender}", userMessage.SenderName);

            var state = await _stateStore.LoadAsync(cancellationToken);
            state.History.Add(userMessage);
            TrimHistory(state);

            var published = new List<RoomMessage>();
            var decision = await _moderatorSelector.SelectAsync(state.History, userMessage, cancellationToken);
            _logger.LogInformation("Applying routing decision: action={Action}, speaker={Speaker}, maxWords={MaxWords}, lastSpeaker={LastSpeaker}, reason={Reason}", decision.Action, decision.Speaker, decision.MaxWords, state.LastSpeaker, decision.Reason);
            if (decision.Action is RouteAction.Silence or RouteAction.Close || decision.Speaker == AgentProfile.None)
            {
                _logger.LogInformation("Staying silent. Action={Action}, Speaker={Speaker}", decision.Action, decision.Speaker);
                state.LastTurnId = userMessage.Id;
                await _stateStore.SaveAsync(state, cancellationToken);
                return new OrchestrationTurnResult { StayedSilent = true };
            }

            if (!RoomRules.CanSpeak(decision.Speaker, state.LastSpeaker))
            {
                _logger.LogInformation("Suppressing routed speaker {Speaker} because last speaker was {LastSpeaker}", decision.Speaker, state.LastSpeaker);
                state.LastTurnId = userMessage.Id;
                await _stateStore.SaveAsync(state, cancellationToken);
                return new OrchestrationTurnResult { StayedSilent = true };
            }

            var firstReply = await _agentInvoker.InvokeAsync(decision.Speaker, state.History, userMessage, decision.MaxWords, cancellationToken);
            if (!string.IsNullOrWhiteSpace(firstReply) && !_repetitionGuard.IsTooSimilar(firstReply, state.History))
            {
                var firstMessage = CreateAgentMessage(decision.Speaker, firstReply, userMessage.Id);
                await SafePublishAsync(firstMessage, cancellationToken);
                state.History.Add(firstMessage);
                state.LastSpeaker = decision.Speaker;
                published.Add(firstMessage);
            }
            else
            {
                _logger.LogInformation("First reply suppressed for {Speaker}. Empty={IsEmpty}", decision.Speaker, string.IsNullOrWhiteSpace(firstReply));
            }

            if (published.Count < _options.MaxPhilosopherRepliesPerTurn && !string.IsNullOrWhiteSpace(firstReply))
            {
                var contrastSpeaker = RoomRules.GetContrastSpeaker(decision.Speaker);
                var shouldAddContrast = _contrastPolicy.ShouldAddContrast(decision.Speaker, firstReply, userMessage, state.LastSpeaker);
                _logger.LogInformation("Contrast evaluation: firstSpeaker={FirstSpeaker}, contrastSpeaker={ContrastSpeaker}, shouldAddContrast={ShouldAddContrast}, publishedCount={PublishedCount}, maxReplies={MaxReplies}", decision.Speaker, contrastSpeaker, shouldAddContrast, published.Count, _options.MaxPhilosopherRepliesPerTurn);
                if (shouldAddContrast)
                {
                    var secondReply = await _agentInvoker.InvokeAsync(contrastSpeaker, state.History, userMessage, decision.MaxWords, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(secondReply) && !_repetitionGuard.IsTooSimilar(secondReply, state.History))
                    {
                        var secondMessage = CreateAgentMessage(contrastSpeaker, secondReply, userMessage.Id);
                        await SafePublishAsync(secondMessage, cancellationToken);
                        state.History.Add(secondMessage);
                        state.LastSpeaker = contrastSpeaker;
                        published.Add(secondMessage);
                    }
                    else
                    {
                        _logger.LogInformation("Contrast reply suppressed for {Speaker}. Empty={IsEmpty}", contrastSpeaker, string.IsNullOrWhiteSpace(secondReply));
                    }
                }
                else
                {
                    _logger.LogInformation("Skipping contrast reply");
                }
            }

            var shouldSummarize = published.Count >= 2;
            _logger.LogInformation("Moderator summary decision: shouldSummarize={ShouldSummarize}, publishedCount={PublishedCount}", shouldSummarize, published.Count);
            if (shouldSummarize)
            {
                var summary = await _agentInvoker.InvokeAsync(AgentProfile.Moderator, state.History, userMessage, _options.ModeratorSummaryWords, cancellationToken);
                if (!string.IsNullOrWhiteSpace(summary) && !_repetitionGuard.IsTooSimilar(summary, state.History))
                {
                    var summaryMessage = CreateAgentMessage(AgentProfile.Moderator, summary, userMessage.Id);
                    await SafePublishAsync(summaryMessage, cancellationToken);
                    state.History.Add(summaryMessage);
                    published.Add(summaryMessage);
                }
                else
                {
                    _logger.LogInformation("Moderator summary suppressed. Empty={IsEmpty}", string.IsNullOrWhiteSpace(summary));
                }
            }

            state.LastTurnId = userMessage.Id;
            await _stateStore.SaveAsync(state, cancellationToken);
            return new OrchestrationTurnResult
            {
                PublishedMessages = published,
                PublishedSummary = shouldSummarize,
                StayedSilent = published.Count == 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to orchestrate incoming message");
            throw;
        }
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

    private async Task SafePublishAsync(RoomMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await _telegramPublisher.PublishAsync(message, cancellationToken);
            _logger.LogInformation("Published {Speaker} message {MessageId}", message.ProducedBy, message.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish {Speaker} message {MessageId}", message.ProducedBy, message.Id);
            throw;
        }
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
