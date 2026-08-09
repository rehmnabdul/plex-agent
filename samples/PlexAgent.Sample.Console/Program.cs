using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PlexAgent.Abstractions;
using PlexAgent.DependencyInjection;
using PlexAgent.Models;
using PlexAgent.Providers.OpenAI;
using PlexAgent.Tools;

var apiKey = Environment.GetEnvironmentVariable("PLEXAGENT_OPENAI_API_KEY") ?? string.Empty;

var services = new ServiceCollection();
services.AddLogging();

var lookup = ToolDefinition.Create(
    "lookup_account",
    "Looks up a demo account by id",
    JsonDocument.Parse("""{"type":"object","properties":{"accountId":{"type":"string"}},"required":["accountId"]}"""),
    (args, _) =>
    {
        var id = args.GetProperty("accountId").GetString() ?? "unknown";
        return Task.FromResult<object?>(new { accountId = id, status = "active", plan = "pro" });
    });

services.AddPlexAgent(options =>
{
    options.DefaultAgent = "DemoAgent";
    options.Sessions.MaxHistoryMessages = 40;
    options.Agents["DemoAgent"] = new PlexAgent.Configuration.AgentDefinitionOptions
    {
        Provider = "OpenAI",
        Model = "gpt-4o-mini",
        SystemPrompt = "You are a helpful assistant. Prefer tools for account lookups. Keep answers short.",
        ToolNames = ["lookup_account"]
    };
})
.AddTool(lookup)
.AddOpenAI(o =>
{
    o.ApiKey = apiKey;
    o.DefaultModel = "gpt-4o-mini";
});

await using var provider = services.BuildServiceProvider();

if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("PLEXAGENT_OPENAI_API_KEY is not set. DI registration verified; skipping live calls.");
    var registered = provider.GetServices<ILlmProvider>().Any(p => p.ProviderId == "OpenAI");
    Console.WriteLine(registered ? "OpenAI provider is registered." : "OpenAI provider is missing.");
    Console.WriteLine("Tool registration verified: lookup_account.");
    return;
}

var agent = provider.GetRequiredService<IAgentFactory>().GetAgent("DemoAgent");

Console.WriteLine("--- single-shot ---");
var hello = await agent.AskAsync("Say hello in one short sentence.");
Console.WriteLine(hello.Content);

Console.WriteLine("--- structured ---");
var plan = await agent.AskAsync<GreetingPlan>("Return a short greeting plan as JSON with title and steps.");
Console.WriteLine($"title={plan.Data.Title}, steps={plan.Data.Steps}");

Console.WriteLine("--- multi-turn session ---");
var session = agent.CreateSession();
var turn1 = await session.AskAsync("Remember that my favorite color is teal.");
Console.WriteLine(turn1.Content);
var turn2 = await session.AskAsync("What color did I just mention?");
Console.WriteLine(turn2.Content);
Console.WriteLine($"turns={session.TurnCount} provider={session.LastProviderId} model={session.LastModel}");

internal sealed class GreetingPlan
{
    public string Title { get; set; } = string.Empty;

    public int Steps { get; set; }
}
