# Plex Agent

[![CI](https://github.com/rehmnabdul/plex-agent/actions/workflows/ci.yml/badge.svg)](https://github.com/rehmnabdul/plex-agent/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/PlexAgent.svg)](https://www.nuget.org/packages/PlexAgent)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**Plex Agent** is a lightweight multi-LLM agent library for **.NET 8+**, ASP.NET Core, and generic hosts.

Configure named agents once, then call a unified API across **OpenAI**, **Anthropic**, and **Google Gemini**:

- `AskAsync` / `AskAsync<T>` for text and structured JSON
- `StreamAsync` for incremental tokens
- Library-owned **tool calling** with schema validation
- Multi-turn **sessions** with history management

Current release: **1.1.0**

---

## Table of contents

- [Why Plex Agent?](#why-plex-agent)
- [Features](#features)
- [Packages](#packages)
- [Requirements](#requirements)
- [Installation](#installation)
- [Quick start](#quick-start)
- [Configuration](#configuration)
- [Usage guide](#usage-guide)
  - [Resolve an agent](#resolve-an-agent)
  - [Ask (text)](#ask-text)
  - [Structured output (`AskAsync<T>`)](#structured-output-askasynct)
  - [Streaming](#streaming)
  - [Tools](#tools)
  - [Sessions](#sessions)
  - [Per-call provider / model overrides](#per-call-provider--model-overrides)
- [Providers](#providers)
- [Samples](#samples)
- [Exceptions](#exceptions)
- [Building and testing](#building-and-testing)
- [Versioning and publishing](#versioning-and-publishing)
- [Contributing](#contributing)
- [License](#license)

---

## Why Plex Agent?

Most apps end up wrapping each LLM SDK differently. Plex Agent gives you:

| Concern | Approach |
| --- | --- |
| Multi-provider | One `IAgent` surface; swap OpenAI / Anthropic / Gemini via config |
| Tools | Core owns the tool loop (call → execute → continue) with max-iteration guard |
| Structured JSON | `AskAsync<T>` generates a schema from `T` when possible |
| Streaming | `IAsyncEnumerable<AgentStreamEvent>` with the same tool loop |
| Hosting | First-class `Microsoft.Extensions.DependencyInjection` integration |

Provider SDKs stay behind adapters — your app code depends on `PlexAgent` types, not OpenAI/Anthropic/Gemini client types.

---

## Features

- Unified `IAgent` / `IAgentFactory` / `IAgentSession` APIs
- Provider packages: OpenAI, Anthropic, Gemini
- Tool calling with JSON Schema argument validation
- Structured output (`AskAsync<T>`, explicit schemas, capability-aware fallback)
- Streaming (`StreamAsync`) across all three providers
- Sessions: system prompt once, `Clear` / `Reset`, turn tracking, history limits
- Per-call overrides: provider, model, temperature, response format
- HttpClient resilience via `Microsoft.Extensions.Http.Resilience`
- MIT licensed, symbol packages (`snupkg`) published with releases

---

## Packages

Install only what you need. The core package never pulls unused provider SDKs.

| Package | NuGet | Description |
| --- | --- | --- |
| [`PlexAgent`](https://www.nuget.org/packages/PlexAgent) | Core | Orchestration, DI, tools, sessions, structured output, streaming |
| [`PlexAgent.Providers.OpenAI`](https://www.nuget.org/packages/PlexAgent.Providers.OpenAI) | OpenAI | Chat Completions adapter (official OpenAI .NET SDK) |
| [`PlexAgent.Providers.Anthropic`](https://www.nuget.org/packages/PlexAgent.Providers.Anthropic) | Anthropic | Messages API adapter (HTTP) |
| [`PlexAgent.Providers.Gemini`](https://www.nuget.org/packages/PlexAgent.Providers.Gemini) | Gemini | `generateContent` / streaming adapter (HTTP) |

**Target framework:** `net8.0`

---

## Requirements

- .NET 8 SDK or later
- An API key for at least one provider you enable
- ASP.NET Core / generic host optional (works with plain `ServiceCollection`)

---

## Installation

```bash
dotnet add package PlexAgent
dotnet add package PlexAgent.Providers.OpenAI
# optional:
dotnet add package PlexAgent.Providers.Anthropic
dotnet add package PlexAgent.Providers.Gemini
```

Or via Package Manager:

```powershell
Install-Package PlexAgent
Install-Package PlexAgent.Providers.OpenAI
```

---

## Quick start

```csharp
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PlexAgent.Abstractions;
using PlexAgent.Configuration;
using PlexAgent.DependencyInjection;
using PlexAgent.Providers.OpenAI;
using PlexAgent.Tools;

var services = new ServiceCollection();
services.AddLogging();

var lookup = ToolDefinition.Create(
    "lookup_account",
    "Looks up an account by id",
    JsonDocument.Parse("""
    {
      "type": "object",
      "properties": { "accountId": { "type": "string" } },
      "required": ["accountId"],
      "additionalProperties": false
    }
    """),
    (args, _) =>
    {
        var id = args.GetProperty("accountId").GetString();
        return Task.FromResult<object?>(new { accountId = id, status = "active" });
    });

services.AddPlexAgent(options =>
{
    options.Agents["Support"] = new AgentDefinitionOptions
    {
        Provider = "OpenAI",
        Model = "gpt-4o-mini",
        SystemPrompt = "Be concise. Use tools when you need account data.",
        ToolNames = ["lookup_account"]
    };
})
.AddTool(lookup)
.AddOpenAI(o =>
{
    o.ApiKey = Environment.GetEnvironmentVariable("PLEXAGENT_OPENAI_API_KEY")!;
    o.DefaultModel = "gpt-4o-mini";
});

await using var sp = services.BuildServiceProvider();
var agent = sp.GetRequiredService<IAgentFactory>().GetAgent("Support");

var answer = await agent.AskAsync("Look up account A-42 and summarize status.");
Console.WriteLine(answer.Content);
```

---

## Configuration

### Code-first

```csharp
services.AddPlexAgent(options =>
{
    options.DefaultAgent = "Support";
    options.ToolLoop.MaxIterations = 10;
    options.Sessions.MaxHistoryMessages = 40; // 0 = unlimited
    options.EnableSensitiveLogging = false;

    options.Agents["Support"] = new AgentDefinitionOptions
    {
        Provider = "OpenAI",          // or "Anthropic", "Gemini"
        Model = "gpt-4o-mini",
        SystemPrompt = "You are a helpful support agent.",
        ResponseFormat = "Text",      // Text | JsonObject | JsonSchema
        ToolNames = ["lookup_account"],
        Parameters =
        {
            Temperature = 0.2f,
            MaxTokens = 1024
        }
    };
});
```

### `appsettings.json`

```json
{
  "PlexAgent": {
    "DefaultAgent": "Support",
    "ToolLoop": {
      "MaxIterations": 10
    },
    "Sessions": {
      "MaxHistoryMessages": 40
    },
    "EnableSensitiveLogging": false,
    "Agents": {
      "Support": {
        "Provider": "OpenAI",
        "Model": "gpt-4o-mini",
        "SystemPrompt": "You are a helpful support agent.",
        "ToolNames": [ "lookup_account" ],
        "Parameters": {
          "Temperature": 0.2,
          "MaxTokens": 1024
        }
      }
    },
    "Providers": {
      "OpenAI": {
        "ApiKey": "",
        "DefaultModel": "gpt-4o-mini"
      },
      "Anthropic": {
        "ApiKey": "",
        "DefaultModel": "claude-3-5-haiku-latest"
      },
      "Gemini": {
        "ApiKey": "",
        "DefaultModel": "gemini-1.5-flash"
      }
    }
  }
}
```

Wire configuration:

```csharp
builder.Services
    .AddPlexAgent(builder.Configuration)
    .AddOpenAI(builder.Configuration.GetSection("PlexAgent:Providers:OpenAI"));
```

### Options reference

| Option | Default | Meaning |
| --- | --- | --- |
| `DefaultAgent` | `null` | Optional default agent name for host apps |
| `ToolLoop.MaxIterations` | `10` | Max model↔tool rounds per request |
| `Sessions.MaxHistoryMessages` | `0` | Max retained session messages (`0` = unlimited); drops oldest non-system messages |
| `EnableSensitiveLogging` | `false` | When `true`, may log prompts/tool payloads |
| `Agents[name].Provider` | required | Provider id: `OpenAI`, `Anthropic`, `Gemini` |
| `Agents[name].Model` | required | Model id for that agent |
| `Agents[name].SystemPrompt` | `null` | Injected once per session / prepared request |
| `Agents[name].ToolNames` | `[]` | Registered tool names this agent may call |
| `Agents[name].ResponseFormat` | `Text` | Default response format name |

---

## Usage guide

### Resolve an agent

```csharp
var factory = sp.GetRequiredService<IAgentFactory>();
var agent = factory.GetAgent("Support");          // throws AgentNotFoundException if missing
factory.TryGetAgent("support", out var maybe);    // case-insensitive
```

Agents are **scoped** (`IAgentFactory` is scoped). Resolve them from a scope in ASP.NET Core / workers.

### Ask (text)

```csharp
var response = await agent.AskAsync("How do I reset my password?");

Console.WriteLine(response.Content);
Console.WriteLine($"{response.ProviderId}/{response.Model}");
Console.WriteLine(response.FinishReason);
Console.WriteLine(response.Usage?.TotalTokens);
```

You can also pass an explicit message list:

```csharp
var response = await agent.AskAsync(
[
    AgentMessage.System("Stay brief."),
    AgentMessage.User("Summarize our refund policy.")
]);
```

### Structured output (`AskAsync<T>`)

```csharp
public sealed class ResetPlan
{
    public string Title { get; set; } = "";
    public int Steps { get; set; }
    public string? Notes { get; set; }
}

var result = await agent.AskAsync<ResetPlan>(
    "Return a password-reset plan as JSON with title and steps.");

Console.WriteLine(result.Data.Title);
Console.WriteLine(result.Data.Steps);
```

Behavior:

1. Generates a JSON Schema from `T` (reflection) unless you supply one
2. Prefers provider **JSON Schema** mode when supported
3. Falls back to **JSON object** mode + post-validation when needed
4. Validates types / `required` / `enum` / bounds / `additionalProperties`
5. Deserializes into `AgentResponse<T>.Data`

Explicit schema:

```csharp
var schema = JsonSchemaGenerator.FromType<ResetPlan>(name: "reset_plan");
var result = await agent.AskAsync<ResetPlan>("...", schema);
```

Or via options:

```csharp
await agent.AskAsync("...", opts => opts.WithJsonSchemaFrom<ResetPlan>());
await agent.AskAsync("...", opts => opts.WithJsonObject());
```

### Streaming

```csharp
await foreach (var evt in agent.StreamAsync("Write a short welcome message."))
{
    switch (evt.Kind)
    {
        case AgentStreamEventKind.ContentDelta:
            Console.Write(evt.TextDelta);
            break;

        case AgentStreamEventKind.ToolCall:
            Console.WriteLine($"\n[tool call] {evt.ToolCall!.Name}");
            break;

        case AgentStreamEventKind.ToolResult:
            Console.WriteLine($"[tool result] {evt.ToolExecution!.Name}");
            break;

        case AgentStreamEventKind.Completed:
            Console.WriteLine($"\n--- done: {evt.Response!.FinishReason} ---");
            break;
    }
}
```

If the provider does not support streaming, `StreamAsync` throws `StreamingNotSupportedException`.

### Tools

Register tools on the builder, then reference them by name on agents.

```csharp
var weather = ToolDefinition.Create(
    "get_weather",
    "Gets the weather for a city",
    JsonDocument.Parse("""
    {
      "type": "object",
      "properties": {
        "city": { "type": "string", "minLength": 1 }
      },
      "required": ["city"],
      "additionalProperties": false
    }
    """),
    async (args, ct) =>
    {
        var city = args.GetProperty("city").GetString();
        // call your API...
        return new { city, tempC = 22, condition = "sunny" };
    });

services.AddPlexAgent(o =>
{
    o.Agents["Concierge"] = new AgentDefinitionOptions
    {
        Provider = "OpenAI",
        Model = "gpt-4o-mini",
        ToolNames = ["get_weather"]
    };
})
.AddTool(weather)
.AddOpenAI(...);
```

After a turn:

```csharp
var response = await agent.AskAsync("What's the weather in Lisbon?");
foreach (var tool in response.ToolsExecuted ?? [])
{
    Console.WriteLine($"{tool.Name}: {tool.ResultJson} (error={tool.IsError})");
}
```

Notes:

- Tool arguments are validated against the schema before your handler runs
- The tool loop stops with `ToolLoopMaxIterationsExceededException` if the model keeps calling tools past `ToolLoop.MaxIterations`
- Unknown tool names throw `ToolNotFoundException`
- Providers that cannot call tools throw `ToolCallingNotSupportedException` when tools are configured

You can also implement `IToolDefinition` and register with `.AddTool<MyTool>()`.

### Sessions

```csharp
var session = agent.CreateSession();

await session.AskAsync("My name is Ada.");
await session.AskAsync("What is my name?");

Console.WriteLine(session.TurnCount);        // 2
Console.WriteLine(session.LastProviderId); // e.g. OpenAI
Console.WriteLine(session.History.Count);

session.Clear();  // wipe history; system prompt re-injected on next ask
session.Reset();  // clear + immediately restore system prompt
```

Streaming in a session updates history when the turn completes:

```csharp
await foreach (var evt in session.StreamAsync("Tell me a short joke."))
{
    if (evt.Kind == AgentStreamEventKind.ContentDelta)
        Console.Write(evt.TextDelta);
}
```

### Per-call provider / model overrides

```csharp
// switch provider/model for one call (requires that provider registered)
await agent.AskAsync(
    "Summarize this in one sentence.",
    opts => opts.WithProvider("Gemini", "gemini-1.5-flash"));

await session.AskAsync(
    "Continue, but use Claude.",
    opts => opts.WithProvider("Anthropic", "claude-3-5-haiku-latest"));

await agent.AskAsync(
    "Be more creative.",
    opts => opts.WithTemperature(0.9f).WithModel("gpt-4o"));
```

History stays provider-agnostic, so mid-session switches are supported.

---

## Providers

Register one or more providers on the fluent builder:

```csharp
services.AddPlexAgent(configuration)
    .AddOpenAI(o =>
    {
        o.ApiKey = "...";
        o.DefaultModel = "gpt-4o-mini";
        o.OrganizationId = null;      // optional
        o.BaseUrl = null;             // optional custom endpoint
        o.TimeoutSeconds = 120;
    })
    .AddAnthropic(o =>
    {
        o.ApiKey = "...";
        o.DefaultModel = "claude-3-5-haiku-latest";
    })
    .AddGemini(o =>
    {
        o.ApiKey = "...";
        o.DefaultModel = "gemini-1.5-flash";
    });
```

| Capability | OpenAI | Anthropic | Gemini |
| --- | --- | --- | --- |
| Tool calling | Yes | Yes | Yes |
| Streaming | Yes | Yes | Yes |
| JSON object mode | Yes | Yes (prompt guidance) | Yes |
| Strict JSON Schema | Yes | No (falls back to JSON object + validation) | Yes |

Provider ids used in config / overrides: `OpenAI`, `Anthropic`, `Gemini` (also available as `LlmProviderIds.*`).

---

## Samples

| Sample | Path | What it shows |
| --- | --- | --- |
| Console | [`samples/PlexAgent.Sample.Console`](samples/PlexAgent.Sample.Console) | Tools, `AskAsync`, `AskAsync<T>`, multi-turn session |
| Web API | [`samples/PlexAgent.Sample.WebApi`](samples/PlexAgent.Sample.WebApi) | `POST /ask`, `/ask/structured`, `/session/demo` |

```bash
# Console (live OpenAI when key is set)
set PLEXAGENT_OPENAI_API_KEY=sk-...
dotnet run --project samples/PlexAgent.Sample.Console

# Web API
dotnet run --project samples/PlexAgent.Sample.WebApi
```

Without an API key, samples still verify DI registration and exit cleanly.

---

## Exceptions

All library errors derive from `PlexAgentException`.

| Exception | When |
| --- | --- |
| `AgentNotFoundException` | Unknown agent name |
| `ProviderNotRegisteredException` | Provider id not registered in DI |
| `ProviderConfigurationException` | Missing API key / invalid provider config |
| `ProviderRequestException` | Upstream HTTP/API failure |
| `ToolCallingNotSupportedException` | Agent has tools but provider cannot call them |
| `StreamingNotSupportedException` | `StreamAsync` on a non-streaming provider |
| `ToolNotFoundException` | Agent references an unregistered tool |
| `ToolExecutionException` | Invalid tool args or handler failure |
| `ToolLoopMaxIterationsExceededException` | Tool loop exceeded max iterations |
| `StructuredOutputNotSupportedException` | Provider cannot produce requested JSON mode |
| `StructuredOutputException` | Invalid JSON / schema validation / deserialize failure (`RawContent` available) |

---

## Building and testing

```bash
dotnet restore PlexAgent.sln
dotnet build PlexAgent.sln -c Release
dotnet test PlexAgent.sln -c Release --filter "FullyQualifiedName!~IntegrationTests"
```

### Live OpenAI integration tests

```bash
# Windows PowerShell
$env:PLEXAGENT_OPENAI_API_KEY = "sk-..."
dotnet test tests/PlexAgent.IntegrationTests -c Release
```

Without the key, tests are **skipped** (not failed).

CI:

- `build-test` — restore, build, unit tests, pack
- `integration-openai` — runs live tests when `PLEXAGENT_OPENAI_API_KEY` secret is set

---

## Versioning and publishing

- Version is centralized in [`build/Directory.Build.props`](build/Directory.Build.props)
- Packages include README, license, and symbols (`snupkg`)
- Push a `v*` tag or publish a GitHub Release to trigger [`.github/workflows/publish.yml`](.github/workflows/publish.yml)
- Requires repo secret `NUGET_API_KEY`

```bash
git tag v1.1.0
git push origin v1.1.0
```

---

## Project layout

```text
src/
  PlexAgent/                         # core library
  PlexAgent.Providers.OpenAI/
  PlexAgent.Providers.Anthropic/
  PlexAgent.Providers.Gemini/
samples/
  PlexAgent.Sample.Console/
  PlexAgent.Sample.WebApi/
tests/
  PlexAgent.Tests/
  PlexAgent.Providers.*.Tests/
  PlexAgent.IntegrationTests/
```

---

## Contributing

1. Fork and create a feature branch
2. Keep changes focused; match existing style
3. Add/adjust unit tests for new behavior
4. Run the focused test projects above before opening a PR
5. Open a PR against `main`

Issues and PRs are welcome.

---

## License

MIT © Abdur Rahman — see [LICENSE](LICENSE).
