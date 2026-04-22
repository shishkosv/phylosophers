using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orkestrator.Config;
using Orkestrator.Models;
using Orkestrator.Services;
using Xunit;

namespace Orkestrator.Tests;

public sealed class OpenClawBridgeWorkerTests
{
    [Fact]
    public async Task ProcessAsync_UsesFallback_WhenSessionKeyMissing_AndFallbackEnabled()
    {
        var options = Options.Create(new OrchestratorOptions
        {
            OpenClaw = new OpenClawOptions
            {
                EnablePromptEchoFallback = true
            }
        });

        var worker = new OpenClawBridgeWorker(new StubGatewayClient("unused"), options, NullLogger<OpenClawBridgeWorker>.Instance);
        var response = await worker.ProcessAsync(new OpenClawBridgeRequest
        {
            Kind = "agent",
            Profile = AgentProfile.Aristotle,
            Prompt = "hello"
        });

        Assert.True(response.Ok);
        Assert.Contains("Aristotle", response.Reply);
        Assert.Contains("hello", response.Reply);
    }

    [Fact]
    public async Task ProcessAsync_ForwardsToGateway_WhenSessionKeyAvailable()
    {
        var options = Options.Create(new OrchestratorOptions
        {
            OpenClaw = new OpenClawOptions
            {
                SessionKey = "agent:test",
                EnablePromptEchoFallback = false
            }
        });

        var worker = new OpenClawBridgeWorker(new StubGatewayClient("{\"reply\":\"from gateway\"}"), options, NullLogger<OpenClawBridgeWorker>.Instance);
        var response = await worker.ProcessAsync(new OpenClawBridgeRequest
        {
            Kind = "agent",
            Profile = AgentProfile.Aristotle,
            Prompt = "hello"
        });

        Assert.True(response.Ok);
        Assert.Equal("from gateway", response.Reply);
        Assert.Equal("{\"reply\":\"from gateway\"}", response.Raw);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsErrorResponse_WhenGatewayThrows()
    {
        var options = Options.Create(new OrchestratorOptions
        {
            OpenClaw = new OpenClawOptions
            {
                SessionKey = "agent:test",
                EnablePromptEchoFallback = false
            }
        });

        var worker = new OpenClawBridgeWorker(new ThrowingGatewayClient(), options, NullLogger<OpenClawBridgeWorker>.Instance);
        var response = await worker.ProcessAsync(new OpenClawBridgeRequest
        {
            Kind = "agent",
            Profile = AgentProfile.Aristotle,
            Prompt = "hello"
        });

        Assert.False(response.Ok);
        Assert.Equal("gateway failed", response.Error);
    }

    private sealed class StubGatewayClient(string response) : IOpenClawGatewayClient
    {
        public Task<string?> SendAsync(string sessionKey, string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(response);
    }

    private sealed class ThrowingGatewayClient : IOpenClawGatewayClient
    {
        public Task<string?> SendAsync(string sessionKey, string prompt, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("gateway failed");
    }
}
