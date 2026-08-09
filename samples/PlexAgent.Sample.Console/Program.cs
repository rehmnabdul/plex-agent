using Microsoft.Extensions.DependencyInjection;
using PlexAgent.DependencyInjection;
using PlexAgent.Providers.OpenAI;

// Phase 0 scaffold sample — full agent usage lands in later phases.
var services = new ServiceCollection();
services.AddPlexAgent(options =>
{
    options.DefaultAgent = "DemoAgent";
    options.Agents["DemoAgent"] = new PlexAgent.Configuration.AgentDefinitionOptions
    {
        Provider = "OpenAI",
        Model = "gpt-4o-mini",
        SystemPrompt = "You are a helpful assistant."
    };
}).AddOpenAI(o =>
{
    o.ApiKey = Environment.GetEnvironmentVariable("PLEXAGENT_OPENAI_API_KEY") ?? string.Empty;
    o.DefaultModel = "gpt-4o-mini";
});

Console.WriteLine("Plex Agent sample console scaffolded. Agent runtime arrives in Phase 1+.");
