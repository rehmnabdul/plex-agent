using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlexAgent.Abstractions;
using PlexAgent.Configuration;
using PlexAgent.DependencyInjection;
using PlexAgent.Models;
using PlexAgent.Tools;

namespace PlexAgent.Tests;

public class SessionTests
{
    [Fact]
    public async Task Clear_ReinjectsSystemPromptOnNextTurn()
    {
        using var provider = Phase1TestHost.Create(options =>
        {
            options.Agents["SupportAgent"] = new AgentDefinitionOptions
            {
                Provider = "Fake",
                Model = "fake-1",
                SystemPrompt = "System rules"
            };
        });

        var session = provider.GetRequiredService<IAgentFactory>().GetAgent("SupportAgent").CreateSession();
        await session.AskAsync("One");
        Assert.Equal(1, session.TurnCount);

        session.Clear();
        Assert.Empty(session.History);
        Assert.Equal(0, session.TurnCount);
        Assert.Null(session.LastProviderId);

        await session.AskAsync("Two");
        Assert.Equal(1, session.History.Count(m => m.Role == AgentRole.System));
        Assert.Equal(1, session.TurnCount);
        Assert.Equal("Fake", session.LastProviderId);
        Assert.Equal("fake-1", session.LastModel);
    }

    [Fact]
    public void Reset_ImmediatelyRestoresSystemPrompt()
    {
        using var provider = Phase1TestHost.Create(options =>
        {
            options.Agents["SupportAgent"] = new AgentDefinitionOptions
            {
                Provider = "Fake",
                Model = "fake-1",
                SystemPrompt = "System rules"
            };
        });

        var session = provider.GetRequiredService<IAgentFactory>().GetAgent("SupportAgent").CreateSession();
        session.Reset();
        Assert.Single(session.History);
        Assert.Equal(AgentRole.System, session.History[0].Role);
    }

    [Fact]
    public async Task AskAsync_TrimsHistoryToMaxMessages()
    {
        using var provider = Phase1TestHost.Create(options =>
        {
            options.Sessions.MaxHistoryMessages = 5;
            options.Agents["SupportAgent"] = new AgentDefinitionOptions
            {
                Provider = "Fake",
                Model = "fake-1",
                SystemPrompt = "System rules"
            };
        });

        var session = provider.GetRequiredService<IAgentFactory>().GetAgent("SupportAgent").CreateSession();
        await session.AskAsync("One");
        await session.AskAsync("Two");
        await session.AskAsync("Three");

        // system + (user+assistant)*3 would be 7; trim to 5 keeping system
        Assert.True(session.History.Count <= 5);
        Assert.Equal(AgentRole.System, session.History[0].Role);
        Assert.Contains(session.History, m => m.Role == AgentRole.User && m.Content == "Three");
    }

    [Fact]
    public async Task AskAsync_MidSessionProviderOverride_UpdatesLastProvider()
    {
        var services = Phase1TestHost.CreateServices(options =>
        {
            options.Agents["SupportAgent"] = new AgentDefinitionOptions
            {
                Provider = "Fake",
                Model = "fake-1",
                SystemPrompt = "System rules"
            };
        }, registerFake: false);
        services.AddSingleton<ILlmProvider>(new FakeLlmProvider("Fake", "fake-1"));
        services.AddSingleton<ILlmProvider>(new FakeLlmProvider("Other", "other-1"));

        using var provider = services.BuildServiceProvider();
        var session = provider.GetRequiredService<IAgentFactory>().GetAgent("SupportAgent").CreateSession();

        await session.AskAsync("default provider");
        Assert.Equal("Fake", session.LastProviderId);
        Assert.Equal("fake-1", session.LastModel);

        await session.AskAsync("switch", opts => opts.WithProvider("Other", "other-1"));
        Assert.Equal("Other", session.LastProviderId);
        Assert.Equal("other-1", session.LastModel);
        Assert.Equal(2, session.TurnCount);
        Assert.Equal(1, session.History.Count(m => m.Role == AgentRole.System));
    }

    [Fact]
    public async Task AskAsync_WithTools_KeepsToolMessagesInHistory()
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
                ]
            },
            new ProviderCompletionResult
            {
                Content = "Account A-1 is active.",
                Model = "fake-1",
                FinishReason = AgentFinishReason.Stop
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

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));
        var builder = services.AddPlexAgent(options =>
        {
            options.Agents["SupportAgent"] = new AgentDefinitionOptions
            {
                Provider = "Fake",
                Model = "fake-1",
                SystemPrompt = "Use tools when needed.",
                ToolNames = ["lookup_account"]
            };
        });
        builder.AddTool(lookup);
        services.AddSingleton<ILlmProvider>(scripted);

        using var provider = services.BuildServiceProvider();
        var session = provider.GetRequiredService<IAgentFactory>().GetAgent("SupportAgent").CreateSession();
        var response = await session.AskAsync("Check A-1");

        Assert.Equal("Account A-1 is active.", response.Content);
        Assert.Contains(session.History, m => m.Role == AgentRole.Tool);
        Assert.NotNull(response.ToolsExecuted);
        Assert.Equal(1, session.TurnCount);
    }

    [Fact]
    public async Task AskAsyncT_InSession_DeserializesAndTracksProvider()
    {
        var fake = new FakeLlmProvider("Fake", "fake-1", """{"title":"Reset","steps":2}""");
        var services = Phase1TestHost.CreateServices(options =>
        {
            options.Agents["Extractor"] = new AgentDefinitionOptions
            {
                Provider = "Fake",
                Model = "fake-1",
                SystemPrompt = "Extract JSON."
            };
        }, registerFake: false);
        services.AddSingleton<ILlmProvider>(fake);

        using var provider = services.BuildServiceProvider();
        var session = provider.GetRequiredService<IAgentFactory>().GetAgent("Extractor").CreateSession();
        var response = await session.AskAsync<ResetPlan>("extract");

        Assert.Equal("Reset", response.Data.Title);
        Assert.Equal(2, response.Data.Steps);
        Assert.Equal("Fake", session.LastProviderId);
        Assert.Equal(ResponseFormatKind.JsonSchema, fake.LastResponseFormat);
    }

    private sealed class ResetPlan
    {
        public string Title { get; set; } = string.Empty;

        public int Steps { get; set; }
    }
}
