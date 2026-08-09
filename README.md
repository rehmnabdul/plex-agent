# Plex Agent

Multi-LLM agent library for ASP.NET Core and .NET generic hosts. Configure named agents once, then call `AskAsync` / `AskAsync<T>` with optional tools and multi-turn sessions.

## Packages

| Package | Purpose |
| --- | --- |
| `PlexAgent` | Core orchestration, DI, tools, sessions, structured output |
| `PlexAgent.Providers.OpenAI` | OpenAI Chat Completions adapter |
| `PlexAgent.Providers.Anthropic` | Anthropic Messages API adapter |
| `PlexAgent.Providers.Gemini` | Google Gemini adapter |

Current version: **1.0.0**

## Quick start

```csharp
services.AddPlexAgent(options =>
{
    options.Agents["Support"] = new AgentDefinitionOptions
    {
        Provider = "OpenAI",
        Model = "gpt-4o-mini",
        SystemPrompt = "Be concise.",
        ToolNames = ["lookup_account"]
    };
})
.AddTool(lookupAccountTool)
.AddOpenAI(o => o.ApiKey = configuration["OpenAI:ApiKey"]!);

var agent = serviceProvider.GetRequiredService<IAgentFactory>().GetAgent("Support");
var answer = await agent.AskAsync("How do I reset my password?");
var structured = await agent.AskAsync<ResetPlan>("Return a reset plan as JSON.");

var session = agent.CreateSession();
await session.AskAsync("My name is Ada.");
await session.AskAsync("What is my name?");
```

## Features

- Unified `IAgent` / `IAgentFactory` API over OpenAI, Anthropic, and Gemini
- Library-owned tool loop with max-iteration guard and argument validation
- Structured JSON via `AskAsync<T>` (JSON Schema when supported, JSON object fallback)
- Multi-turn `IAgentSession` with `Clear` / `Reset`, turn tracking, and optional history limits
- Per-call provider/model overrides (`WithProvider`, `WithModel`)
- HttpClient resilience via `Microsoft.Extensions.Http.Resilience`

## Samples

- `samples/PlexAgent.Sample.Console` — single-shot, structured, session, and tool registration
- `samples/PlexAgent.Sample.WebApi` — `/ask`, `/ask/structured`, `/session/demo`

Set `PLEXAGENT_OPENAI_API_KEY` for live OpenAI calls.

## Build

```bash
dotnet restore PlexAgent.sln
dotnet build PlexAgent.sln -c Release
dotnet test PlexAgent.sln -c Release --filter "FullyQualifiedName!~IntegrationTests"
dotnet pack src/PlexAgent/PlexAgent.csproj -c Release -o artifacts
```

## Publish

Push a `v*` tag or publish a GitHub Release. The `publish` workflow packs all packages and pushes to NuGet.org when `NUGET_API_KEY` is configured.

## License

MIT
