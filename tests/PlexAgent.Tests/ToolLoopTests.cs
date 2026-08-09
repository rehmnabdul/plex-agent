using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlexAgent.Abstractions;
using PlexAgent.Configuration;
using PlexAgent.DependencyInjection;
using PlexAgent.Exceptions;
using PlexAgent.Models;
using PlexAgent.Tools;

namespace PlexAgent.Tests;

public class ToolLoopTests
{
    [Fact]
    public async Task AskAsync_ExecutesToolAndReturnsFinalAnswer()
    {
        var scripted = new ScriptedLlmProvider(
            supportsTools: true,
            new ProviderCompletionResult
            {
                Content = string.Empty,
                Model = "fake-1",
                FinishReason = AgentFinishReason.ToolCalls,
                ToolCalls =
                [
                    new ToolCall
                    {
                        Id = "call_1",
                        Name = "lookup_account",
                        ArgumentsJson = """{"accountId":"A-1"}"""
                    }
                ],
                Usage = new AgentUsage { InputTokens = 5, OutputTokens = 2, TotalTokens = 7 }
            },
            new ProviderCompletionResult
            {
                Content = "Account A-1 is active.",
                Model = "fake-1",
                FinishReason = AgentFinishReason.Stop,
                Usage = new AgentUsage { InputTokens = 8, OutputTokens = 4, TotalTokens = 12 }
            });

        var lookup = ToolDefinition.Create(
            "lookup_account",
            "Looks up an account",
            JsonDocument.Parse("""{"type":"object","properties":{"accountId":{"type":"string"}},"required":["accountId"]}"""),
            (args, _) =>
            {
                var id = args.GetProperty("accountId").GetString();
                return Task.FromResult<object?>(new { status = "active", accountId = id });
            });

        using var provider = CreateHost(scripted, lookup, toolNames: ["lookup_account"]);
        var agent = provider.GetRequiredService<IAgentFactory>().GetAgent("SupportAgent");

        var response = await agent.AskAsync("Check account A-1");

        Assert.Equal("Account A-1 is active.", response.Content);
        Assert.Equal(AgentFinishReason.Stop, response.FinishReason);
        Assert.NotNull(response.ToolsExecuted);
        Assert.Single(response.ToolsExecuted);
        Assert.Equal("lookup_account", response.ToolsExecuted![0].Name);
        Assert.Contains("active", response.ToolsExecuted[0].ResultJson, StringComparison.Ordinal);
        Assert.Equal(2, scripted.CallCount);
        Assert.Equal(13, response.Usage?.InputTokens);
        Assert.Equal(6, response.Usage?.OutputTokens);
        Assert.Contains(response.Messages, m => m.Role == AgentRole.Tool);
    }

    [Fact]
    public async Task AskAsync_WhenProviderDoesNotSupportTools_Throws()
    {
        var scripted = new ScriptedLlmProvider(supportsTools: false);
        var tool = ToolDefinition.Create(
            "noop",
            "No-op",
            JsonDocument.Parse("""{"type":"object","properties":{}}"""),
            (_, _) => Task.FromResult<object?>("ok"));

        using var provider = CreateHost(scripted, tool, toolNames: ["noop"]);
        var agent = provider.GetRequiredService<IAgentFactory>().GetAgent("SupportAgent");

        await Assert.ThrowsAsync<ToolCallingNotSupportedException>(() => agent.AskAsync("hi"));
        Assert.Equal(0, scripted.CallCount);
    }

    [Fact]
    public async Task AskAsync_WhenToolLoopExceedsMax_Throws()
    {
        var foreverTools = new ProviderCompletionResult
        {
            Content = string.Empty,
            Model = "fake-1",
            FinishReason = AgentFinishReason.ToolCalls,
            ToolCalls =
            [
                new ToolCall { Id = "call_x", Name = "noop", ArgumentsJson = "{}" }
            ]
        };

        var scripted = new ScriptedLlmProvider(supportsTools: true, foreverTools, foreverTools, foreverTools);
        var tool = ToolDefinition.Create(
            "noop",
            "No-op",
            JsonDocument.Parse("""{"type":"object","properties":{}}"""),
            (_, _) => Task.FromResult<object?>("ok"));

        using var provider = CreateHost(
            scripted,
            tool,
            toolNames: ["noop"],
            configureOptions: o => o.ToolLoop.MaxIterations = 2);

        var agent = provider.GetRequiredService<IAgentFactory>().GetAgent("SupportAgent");
        var ex = await Assert.ThrowsAsync<ToolLoopMaxIterationsExceededException>(() => agent.AskAsync("loop"));
        Assert.Equal(2, ex.MaxIterations);
        Assert.Equal(2, scripted.CallCount);
    }

    [Fact]
    public async Task AskAsync_WhenRequiredArgMissing_ThrowsToolExecutionException()
    {
        var scripted = new ScriptedLlmProvider(
            supportsTools: true,
            new ProviderCompletionResult
            {
                Content = string.Empty,
                Model = "fake-1",
                FinishReason = AgentFinishReason.ToolCalls,
                ToolCalls =
                [
                    new ToolCall
                    {
                        Id = "call_1",
                        Name = "lookup_account",
                        ArgumentsJson = "{}"
                    }
                ]
            });

        var lookup = ToolDefinition.Create(
            "lookup_account",
            "Looks up an account",
            JsonDocument.Parse("""{"type":"object","properties":{"accountId":{"type":"string"}},"required":["accountId"]}"""),
            (_, _) => Task.FromResult<object?>("should-not-run"));

        using var provider = CreateHost(scripted, lookup, toolNames: ["lookup_account"]);
        var agent = provider.GetRequiredService<IAgentFactory>().GetAgent("SupportAgent");

        var ex = await Assert.ThrowsAsync<ToolExecutionException>(() => agent.AskAsync("bad args"));
        Assert.Equal("lookup_account", ex.ToolName);
    }

    [Fact]
    public async Task AskAsync_WhenToolNameUnknown_ThrowsToolNotFoundException()
    {
        var scripted = new ScriptedLlmProvider(supportsTools: true);
        using var provider = CreateHost(scripted, tool: null, toolNames: ["missing_tool"]);
        var agent = provider.GetRequiredService<IAgentFactory>().GetAgent("SupportAgent");

        await Assert.ThrowsAsync<ToolNotFoundException>(() => agent.AskAsync("hi"));
    }

    private static ServiceProvider CreateHost(
        ILlmProvider llm,
        IToolDefinition? tool,
        IEnumerable<string> toolNames,
        Action<PlexAgentOptions>? configureOptions = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));
        var builder = services.AddPlexAgent(options =>
        {
            options.ToolLoop.MaxIterations = 10;
            options.Agents["SupportAgent"] = new AgentDefinitionOptions
            {
                Provider = llm.ProviderId,
                Model = "fake-1",
                ToolNames = toolNames.ToList()
            };
            configureOptions?.Invoke(options);
        });

        if (tool is not null)
        {
            builder.AddTool(tool);
        }

        services.AddSingleton(llm);
        return services.BuildServiceProvider();
    }
}

internal sealed class ScriptedLlmProvider : ILlmProvider
{
    private readonly Queue<ProviderCompletionResult> _results;
    private readonly bool _supportsTools;

    public ScriptedLlmProvider(bool supportsTools, params ProviderCompletionResult[] results)
    {
        _supportsTools = supportsTools;
        _results = new Queue<ProviderCompletionResult>(results);
        ProviderId = "Fake";
    }

    public string ProviderId { get; }

    public int CallCount { get; private set; }

    public IReadOnlyList<ProviderToolDefinition>? LastTools { get; private set; }

    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProviderCapabilities(
            SupportsToolCalling: _supportsTools,
            SupportsStreaming: true,
            SupportsSystemMessages: true,
            SupportsJsonObject: true,
            SupportsJsonSchema: true,
            SupportedModels: ["fake-1"]));
    }

    public Task<ProviderCompletionResult> CompleteAsync(
        ProviderCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastTools = request.Tools;
        if (_results.Count == 0)
        {
            throw new InvalidOperationException("No scripted LLM responses remaining.");
        }

        return Task.FromResult(_results.Dequeue());
    }

    public async IAsyncEnumerable<ProviderStreamUpdate> StreamAsync(
        ProviderCompletionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var result = await CompleteAsync(request, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(result.Content))
        {
            yield return new ProviderStreamUpdate { TextDelta = result.Content };
        }

        yield return new ProviderStreamUpdate { Completed = result };
    }
}
