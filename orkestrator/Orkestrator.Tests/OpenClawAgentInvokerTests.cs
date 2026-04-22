using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orkestrator.Config;
using Orkestrator.Models;
using Orkestrator.Services;
using Xunit;

namespace Orkestrator.Tests;

public sealed class OpenClawAgentInvokerTests
{
    [Fact]
    public async Task InvokeModeratorAsync_ParsesRouteDecision_FromBridgeJson()
    {
        var bridge = new StubBridge("{\"action\":0,\"speaker\":2,\"reason\":\"ok\",\"maxWords\":77}");
        var options = Options.Create(new OrchestratorOptions
        {
            OpenClaw = new OpenClawOptions
            {
                SessionKey = "agent:moderator:telegram:group:test",
                InternalBridge = new InternalBridgeOptions
                {
                    Url = "http://127.0.0.1:5187",
                    RoutePath = "/internal/openclaw/bridge/invoke"
                }
            }
        });

        var invoker = new OpenClawAgentInvoker(bridge, options, NullLogger<OpenClawAgentInvoker>.Instance);
        var decision = await invoker.InvokeModeratorAsync([], CreateUserMessage("What is virtue?"));

        Assert.Equal(RouteAction.SelectSpeaker, decision.Action);
        Assert.Equal(AgentProfile.Aristotle, decision.Speaker);
        Assert.Equal(77, decision.MaxWords);
    }

    [Fact]
    public async Task InvokeAsync_ExtractsReply_Field_FromBridgeJson()
    {
        var bridge = new StubBridge("{\"reply\":\"A short answer\"}");
        var options = Options.Create(new OrchestratorOptions
        {
            OpenClaw = new OpenClawOptions
            {
                SessionKey = "agent:arist:telegram:group:test",
                InternalBridge = new InternalBridgeOptions
                {
                    Url = "http://127.0.0.1:5187",
                    RoutePath = "/internal/openclaw/bridge/invoke"
                }
            }
        });

        var invoker = new OpenClawAgentInvoker(bridge, options, NullLogger<OpenClawAgentInvoker>.Instance);
        var reply = await invoker.InvokeAsync(AgentProfile.Aristotle, [], CreateUserMessage("Why do people fail?"), 90);

        Assert.Equal("A short answer", reply);
    }

    private static RoomMessage CreateUserMessage(string text) => new()
    {
        Id = "1",
        SenderId = "user",
        SenderName = "User",
        Text = text,
        TimestampUtc = DateTimeOffset.UtcNow,
        IsHuman = true
    };

    private sealed class StubBridge(string response) : IOpenClawBridge
    {
        public Task<string?> SendAgentPromptAsync(AgentProfile profile, string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(response);

        public Task<string?> SendModeratorPromptAsync(string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(response);
    }
}
