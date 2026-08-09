using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlexAgent.Abstractions;
using PlexAgent.Configuration;
using PlexAgent.DependencyInjection;
using PlexAgent.Exceptions;
using PlexAgent.Models;

namespace PlexAgent.Tests;

public class StreamingTests
{
    [Fact]
    public async Task StreamAsync_YieldsContentDeltasAndCompleted()
    {
        var fake = new FakeLlmProvider("Fake", "fake-1");
        var services = Phase1TestHost.CreateServices(options =>
        {
            options.Agents["SupportAgent"] = new AgentDefinitionOptions
            {
                Provider = "Fake",
                Model = "fake-1"
            };
        }, registerFake: false);
        services.AddSingleton<ILlmProvider>(fake);

        using var provider = services.BuildServiceProvider();
        var agent = provider.GetRequiredService<IAgentFactory>().GetAgent("SupportAgent");

        var deltas = new List<string>();
        AgentResponse? completed = null;
        await foreach (var streamEvent in agent.StreamAsync("Hello"))
        {
            if (streamEvent.Kind == AgentStreamEventKind.ContentDelta && streamEvent.TextDelta is not null)
            {
                deltas.Add(streamEvent.TextDelta);
            }

            if (streamEvent.Kind == AgentStreamEventKind.Completed)
            {
                completed = streamEvent.Response;
            }
        }

        Assert.Contains("echo:Hello", string.Join(string.Empty, deltas));
        Assert.NotNull(completed);
        Assert.Equal("echo:Hello", completed!.Content);
        Assert.Equal("Fake", completed.ProviderId);
    }

    [Fact]
    public async Task StreamAsync_WhenStreamingUnsupported_Throws()
    {
        var fake = new NonStreamingFakeProvider();
        var services = Phase1TestHost.CreateServices(options =>
        {
            options.Agents["SupportAgent"] = new AgentDefinitionOptions
            {
                Provider = "Fake",
                Model = "fake-1"
            };
        }, registerFake: false);
        services.AddSingleton<ILlmProvider>(fake);

        using var provider = services.BuildServiceProvider();
        var agent = provider.GetRequiredService<IAgentFactory>().GetAgent("SupportAgent");

        await Assert.ThrowsAsync<StreamingNotSupportedException>(async () =>
        {
            await foreach (var _ in agent.StreamAsync("Hello"))
            {
            }
        });
    }
}

internal sealed class NonStreamingFakeProvider : ILlmProvider
{
    public string ProviderId => "Fake";

    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new ProviderCapabilities(
            SupportsToolCalling: false,
            SupportsStreaming: false,
            SupportsSystemMessages: true,
            SupportsJsonObject: true,
            SupportsJsonSchema: true,
            SupportedModels: ["fake-1"]));

    public Task<ProviderCompletionResult> CompleteAsync(
        ProviderCompletionRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new ProviderCompletionResult
        {
            Content = "ok",
            Model = request.Model,
            FinishReason = AgentFinishReason.Stop
        });

    public async IAsyncEnumerable<ProviderStreamUpdate> StreamAsync(
        ProviderCompletionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotSupportedException("Streaming is disabled for this fake.");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
}
