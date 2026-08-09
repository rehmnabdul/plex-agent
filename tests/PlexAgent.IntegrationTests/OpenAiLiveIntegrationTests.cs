using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlexAgent.Abstractions;
using PlexAgent.Configuration;
using PlexAgent.DependencyInjection;
using PlexAgent.Models;
using PlexAgent.Providers.OpenAI;
using PlexAgent.Tools;
using Xunit;

namespace PlexAgent.IntegrationTests;

public class OpenAiLiveIntegrationTests
{
    private static string? ApiKey => Environment.GetEnvironmentVariable("PLEXAGENT_OPENAI_API_KEY");

    private static bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);

    [SkippableFact]
    public async Task AskAsync_ToolLoop_CompletesWithToolExecution()
    {
        Skip.IfNot(HasApiKey, "PLEXAGENT_OPENAI_API_KEY is not set.");

        var lookup = ToolDefinition.Create(
            "lookup_account",
            "Looks up a demo account by id and returns status",
            JsonDocument.Parse("""{"type":"object","properties":{"accountId":{"type":"string"}},"required":["accountId"],"additionalProperties":false}"""),
            (args, _) =>
            {
                var id = args.GetProperty("accountId").GetString() ?? "unknown";
                return Task.FromResult<object?>(new { accountId = id, status = "active" });
            });

        await using var provider = CreateHost(lookup);
        var agent = provider.GetRequiredService<IAgentFactory>().GetAgent("LiveAgent");
        var response = await agent.AskAsync(
            "Use the lookup_account tool with accountId A-42 and then tell me the status in one short sentence.");

        Assert.False(string.IsNullOrWhiteSpace(response.Content));
        Assert.Equal(LlmProviderIds.OpenAI, response.ProviderId);
        Assert.NotNull(response.ToolsExecuted);
        Assert.Contains(response.ToolsExecuted!, t => t.Name == "lookup_account");
    }

    [SkippableFact]
    public async Task AskAsyncT_StructuredOutput_DeserializesPlan()
    {
        Skip.IfNot(HasApiKey, "PLEXAGENT_OPENAI_API_KEY is not set.");

        await using var provider = CreateHost(tool: null);
        var agent = provider.GetRequiredService<IAgentFactory>().GetAgent("LiveAgent");
        var response = await agent.AskAsync<LiveResetPlan>(
            "Return JSON only for a password reset plan with title and steps (integer between 1 and 5).");

        Assert.False(string.IsNullOrWhiteSpace(response.Data.Title));
        Assert.InRange(response.Data.Steps, 1, 5);
        Assert.Equal(LlmProviderIds.OpenAI, response.ProviderId);
    }

    [SkippableFact]
    public async Task StreamAsync_YieldsContentAndCompleted()
    {
        Skip.IfNot(HasApiKey, "PLEXAGENT_OPENAI_API_KEY is not set.");

        await using var provider = CreateHost(tool: null);
        var agent = provider.GetRequiredService<IAgentFactory>().GetAgent("LiveAgent");

        var deltas = new List<string>();
        AgentResponse? completed = null;
        await foreach (var streamEvent in agent.StreamAsync("Say hello in five words or fewer."))
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

        Assert.NotEmpty(deltas);
        Assert.NotNull(completed);
        Assert.False(string.IsNullOrWhiteSpace(completed!.Content));
    }

    private static ServiceProvider CreateHost(IToolDefinition? tool)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        var builder = services.AddPlexAgent(options =>
        {
            options.Agents["LiveAgent"] = new AgentDefinitionOptions
            {
                Provider = LlmProviderIds.OpenAI,
                Model = "gpt-4o-mini",
                SystemPrompt = "You are a concise test assistant.",
                ToolNames = tool is null ? [] : [tool.Name]
            };
        });

        if (tool is not null)
        {
            builder.AddTool(tool);
        }

        builder.AddOpenAI(o =>
        {
            o.ApiKey = ApiKey!;
            o.DefaultModel = "gpt-4o-mini";
        });

        return services.BuildServiceProvider();
    }

    private sealed class LiveResetPlan
    {
        public string Title { get; set; } = string.Empty;

        public int Steps { get; set; }
    }
}
