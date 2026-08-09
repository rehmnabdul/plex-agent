using Microsoft.Extensions.DependencyInjection;
using PlexAgent.Abstractions;
using PlexAgent.DependencyInjection;
using PlexAgent.Providers.OpenAI;

var apiKey = Environment.GetEnvironmentVariable("PLEXAGENT_OPENAI_API_KEY") ?? string.Empty;

var services = new ServiceCollection();
services.AddLogging();
services.AddPlexAgent(options =>
{
    options.DefaultAgent = "DemoAgent";
    options.Agents["DemoAgent"] = new PlexAgent.Configuration.AgentDefinitionOptions
    {
        Provider = "OpenAI",
        Model = "gpt-4o-mini",
        SystemPrompt = "You are a helpful assistant. Keep answers short."
    };
}).AddOpenAI(o =>
{
    o.ApiKey = apiKey;
    o.DefaultModel = "gpt-4o-mini";
});

await using var provider = services.BuildServiceProvider();

if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("PLEXAGENT_OPENAI_API_KEY is not set. DI registration verified; skipping live AskAsync.");
    var registered = provider.GetServices<ILlmProvider>().Any(p => p.ProviderId == "OpenAI");
    Console.WriteLine(registered ? "OpenAI provider is registered." : "OpenAI provider is missing.");
    return;
}

var agent = provider.GetRequiredService<IAgentFactory>().GetAgent("DemoAgent");
var response = await agent.AskAsync("Say hello in one short sentence.");
Console.WriteLine(response.Content);
Console.WriteLine($"provider={response.ProviderId} model={response.Model}");
