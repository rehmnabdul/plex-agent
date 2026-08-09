# Cursor Prompt: Plan "Plex Agent" — A .NET Multi-LLM Agent Library

## Context / Role
Act as a senior .NET library architect. I want you to create a **detailed implementation plan** (not code yet) for a new open-source NuGet library called **Plex Agent**. Do not start writing code — first produce a plan document (architecture, folder structure, class list, interfaces, extensibility points, and milestones) so I can review it before implementation begins.

## Project Summary
**Plex Agent** is a lightweight ASP.NET Core library that lets developers spin up an "AI agent" in a few lines of setup code, without writing any provider-specific boilerplate for talking to LLMs.

Target: **.NET 8+**, published as a **NuGet package**.

## Core Goals
1. **Multi-provider support (v1 scope only):**
   - OpenAI (ChatGPT)
   - Anthropic (Claude)
   - Google (Gemini)
   - Design the provider layer so a 4th/5th provider can be added later without breaking the public API (strategy/adapter pattern, `ILlmProvider` abstraction).

2. **Zero-boilerplate startup:**
   - Single `AddPlexAgent(...)` extension method on `IServiceCollection` / `WebApplicationBuilder`, following standard ASP.NET Core DI conventions.
   - All provider SDKs, HttpClient setup, retries, auth headers, and request/response mapping are hidden behind the library.
   - API keys and provider settings are configured via `appsettings.json` (with `IOptions<T>` pattern) — but also allow code-based configuration for non-ASP.NET (console/worker) apps.

3. **Agent definition model:**
   - Developer defines one or more "Agents" via config or fluent builder, each with:
     - Agent name (unique key)
     - System prompt / persona
     - Default LLM provider + model
     - Optional tools/functions (only if the underlying provider/model supports function calling / tool use)
     - Optional parameters: temperature, max tokens, top_p, etc.
   - Support **multiple agents** registered in one app (e.g., "SupportAgent", "SummarizerAgent"), resolved by name via DI or a factory (`IAgentFactory.GetAgent("SupportAgent")`).

4. **Runtime model switching:**
   - A default model is set at initialization, but the caller can override the model/provider **per request/prompt** at runtime without reconfiguring the agent (e.g., `agent.Ask(prompt, options => options.UseModel("gpt-4o"))`).
   - Switching should be possible mid-conversation/session too, if feasible.

5. **Simple developer-facing API surface**, something like:
   ```csharp
   // Program.cs
   builder.Services.AddPlexAgent(builder.Configuration);

   // appsettings.json defines providers + agents

   // Usage
   var agent = agentFactory.GetAgent("SupportAgent");
   var response = await agent.AskAsync("How do I reset my password?");

   // Override model for a single call
   var response2 = await agent.AskAsync("Summarize this", opts => opts.WithModel(LlmProvider.Gemini, "gemini-1.5-pro"));
   ```

6. **Tool / function calling support:**
   - Abstract tool definition (name, description, JSON schema for parameters, and a delegate/handler) that maps to each provider's native tool-calling format (OpenAI tools, Anthropic tool use, Gemini function calling).
   - Gracefully no-op or throw a clear, catchable exception if a tool is attached to a model that doesn't support tool calling.

7. **Config-driven + code-driven, both supported:**
   - `appsettings.json` section (e.g., `"PlexAgent"`) for provider keys, default agent, and agent list.
   - Fluent/builder API for defining agents in code for those who don't want config-file coupling.

## What I need from you (Cursor) in the plan

1. **High-level architecture diagram (described in text/ASCII)** showing: DI registration → Agent Factory → Agent → LLM Provider Adapter → Provider SDK/HTTP → Response mapping back to a unified `AgentResponse`.

2. **Proposed project/folder structure** for the NuGet package (e.g., `PlexAgent.Core`, `PlexAgent.Providers.OpenAI`, `PlexAgent.Providers.Anthropic`, `PlexAgent.Providers.Gemini`, or a single package with internal provider folders — recommend which approach and why, considering NuGet dependency bloat if a user only wants one provider).

3. **Key interfaces/abstractions**, at minimum:
   - `ILlmProvider` (unified contract each provider adapter implements)
   - `IAgent` / `IAgentFactory`
   - `IToolDefinition` / tool handler contract
   - `AgentOptions` (per-call overrides)
   - `PlexAgentOptions` (root config binding class) + per-provider options classes
   - Unified request/response DTOs (`AgentRequest`, `AgentResponse`, `AgentMessage`, streaming variant if applicable)

4. **Configuration schema** — a sample `appsettings.json` showing providers, keys, default model, and one or more agent definitions (system prompt, tools, provider/model, parameters).

5. **Extensibility strategy** — how a future provider or a custom/self-hosted model (e.g., local Ollama) could be added by consumers without modifying the core library (plugin-style provider registration).

6. **Error handling & resilience plan** — how to handle missing/invalid API keys, rate limits, provider outages, and unsupported features (e.g., tool calling requested on a model that doesn't support it), including whether to use Polly for retries.

7. **Streaming support** — note whether v1 should support streaming responses (IAsyncEnumerable) per provider, and how that fits the unified API.

8. **Testing strategy** — unit testing the provider adapters with mocked HTTP responses, and an integration test harness that skips if no real API key is set in CI.

9. **NuGet packaging considerations** — versioning strategy, target frameworks, symbol packages, README/package metadata, and whether to ship provider SDKs as `PackageReference` or via raw `HttpClient` calls (recommend one, with tradeoffs).

10. **Milestone/phase breakdown** for implementation (e.g., Phase 1: Core abstractions + config; Phase 2: OpenAI provider; Phase 3: Anthropic + Gemini providers; Phase 4: tool calling; Phase 5: streaming; Phase 6: NuGet publish pipeline).

## Constraints
- Keep the public API surface minimal and intuitive — a developer should be able to get a working agent in under 10 lines of setup.
- Don't leak provider-specific types (e.g., raw OpenAI SDK objects) through the public API — everything should go through Plex Agent's own DTOs.
- Favor conventions already idiomatic in ASP.NET Core (Options pattern, DI extension methods, `ILogger<T>` integration).

## Output format
Produce the plan as a structured Markdown document with headers matching the sections above. Do not write full implementation code — pseudocode or short interface signatures are fine to illustrate the design. End with an open-questions list if anything about the requirements above is ambiguous or needs a decision from me before implementation starts.
