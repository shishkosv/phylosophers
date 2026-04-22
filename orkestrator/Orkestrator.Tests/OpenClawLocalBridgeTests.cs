using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orkestrator.Config;
using Orkestrator.Models;
using Orkestrator.Services;
using Xunit;

namespace Orkestrator.Tests;

public sealed class OpenClawLocalBridgeTests
{
    [Fact]
    public async Task LocalBridge_UsesEchoFallback_WhenRemoteIsNotConfigured()
    {
        var options = Options.Create(new OrchestratorOptions
        {
            OpenClaw = new OpenClawOptions
            {
                EnablePromptEchoFallback = true,
                EndpointPath = string.Empty,
                SessionKey = string.Empty
            }
        });

        var httpBridge = new OpenClawHttpBridge(new HttpClient(new ThrowingHandler()), options);
        var bridge = new OpenClawLocalBridge(httpBridge, options, NullLogger<OpenClawLocalBridge>.Instance);

        var response = await bridge.SendAgentPromptAsync(AgentProfile.Aristotle, "hello world");

        Assert.NotNull(response);
        Assert.Contains("Aristotle", response);
        Assert.Contains("hello world", response);
    }

    [Fact]
    public async Task LocalBridge_Throws_WhenStrictModeAndRemoteMissing()
    {
        var options = Options.Create(new OrchestratorOptions
        {
            OpenClaw = new OpenClawOptions
            {
                EnablePromptEchoFallback = false,
                EndpointPath = string.Empty,
                SessionKey = string.Empty
            }
        });

        var httpBridge = new OpenClawHttpBridge(new HttpClient(new ThrowingHandler()), options);
        var bridge = new OpenClawLocalBridge(httpBridge, options, NullLogger<OpenClawLocalBridge>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => bridge.SendAgentPromptAsync(AgentProfile.Aristotle, "hello world"));
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("HTTP should not be called in fallback tests.");
    }
}
