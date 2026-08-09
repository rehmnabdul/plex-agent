using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlexAgent.Abstractions;
using PlexAgent.Configuration;
using PlexAgent.DependencyInjection;
using PlexAgent.Exceptions;
using PlexAgent.Models;

namespace PlexAgent.Tests;

public class AgentFactoryTests
{
    [Fact]
    public void GetAgent_WhenMissing_ThrowsAgentNotFoundException()
    {
        using var provider = CreateProvider(options =>
        {
            options.Agents["SupportAgent"] = new AgentDefinitionOptions
            {
                Provider = "Fake",
                Model = "fake-1"
            };
        });

        var factory = provider.GetRequiredService<IAgentFactory>();
        Assert.Throws<AgentNotFoundException>(() => factory.GetAgent("Missing"));
    }

    [Fact]
    public void TryGetAgent_IsCaseInsensitive_AndUsesCanonicalName()
    {
        using var provider = CreateProvider(options =>
        {
            options.Agents["SupportAgent"] = new AgentDefinitionOptions
            {
                Provider = "Fake",
                Model = "fake-1"
            };
        });

        var factory = provider.GetRequiredService<IAgentFactory>();
        Assert.True(factory.TryGetAgent("supportagent", out var agent));
        Assert.Equal("SupportAgent", agent!.Name);
    }

    private static ServiceProvider CreateProvider(Action<PlexAgentOptions> configure, bool registerFake = true)
        => Phase1TestHost.Create(configure, registerFake);
}

public class AgentOrchestratorTests
{
    [Fact]
    public async Task AskAsync_WithMockProvider_ReturnsUnifiedResponse()
    {
        using var provider = Phase1TestHost.Create(options =>
        {
            options.Agents["SupportAgent"] = new AgentDefinitionOptions
            {
                Provider = "Fake",
                Model = "fake-1",
                SystemPrompt = "Be helpful."
            };
        });

        var agent = provider.GetRequiredService<IAgentFactory>().GetAgent("SupportAgent");
        var response = await agent.AskAsync("Hello");

        Assert.Equal("echo:Hello", response.Content);
        Assert.Equal("Fake", response.ProviderId);
        Assert.Equal("fake-1", response.Model);
        Assert.Equal(AgentFinishReason.Stop, response.FinishReason);
        Assert.Contains(response.Messages, m => m.Role == AgentRole.Assistant);
    }

    [Fact]
    public async Task AskAsync_WithProviderOverride_UsesRequestedProvider()
    {
        var services = Phase1TestHost.CreateServices(options =>
        {
            options.Agents["SupportAgent"] = new AgentDefinitionOptions
            {
                Provider = "Fake",
                Model = "fake-1"
            };
        });
        services.AddSingleton<ILlmProvider>(new FakeLlmProvider("Other", "other-1"));

        using var provider = services.BuildServiceProvider();
        var agent = provider.GetRequiredService<IAgentFactory>().GetAgent("SupportAgent");

        var response = await agent.AskAsync("Hi", opts => opts.WithProvider("Other", "other-1"));

        Assert.Equal("Other", response.ProviderId);
        Assert.Equal("other-1", response.Model);
        Assert.Equal("echo:Hi", response.Content);
    }

    [Fact]
    public async Task AskAsync_WhenProviderMissing_ThrowsProviderNotRegisteredException()
    {
        using var provider = Phase1TestHost.Create(options =>
        {
            options.Agents["SupportAgent"] = new AgentDefinitionOptions
            {
                Provider = "MissingProvider",
                Model = "x"
            };
        }, registerFake: false);

        var agent = provider.GetRequiredService<IAgentFactory>().GetAgent("SupportAgent");
        await Assert.ThrowsAsync<ProviderNotRegisteredException>(() => agent.AskAsync("Hello"));
    }

    [Fact]
    public async Task AskAsync_Structured_DeserializesPayload()
    {
        var fake = new FakeLlmProvider("Fake", "fake-1", """{"title":"Reset","steps":2}""");
        var services = Phase1TestHost.CreateServices(options =>
        {
            options.Agents["Extractor"] = new AgentDefinitionOptions
            {
                Provider = "Fake",
                Model = "fake-1"
            };
        }, registerFake: false);
        services.AddSingleton<ILlmProvider>(fake);

        using var provider = services.BuildServiceProvider();
        var agent = provider.GetRequiredService<IAgentFactory>().GetAgent("Extractor");
        var response = await agent.AskAsync<ResetPlan>("extract");

        Assert.Equal("Reset", response.Data.Title);
        Assert.Equal(2, response.Data.Steps);
        Assert.Equal(ResponseFormatKind.JsonSchema, fake.LastResponseFormat);
        Assert.NotNull(fake.LastJsonSchema);
    }

    [Fact]
    public async Task CreateSession_InjectsSystemPromptOnce()
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
        await session.AskAsync("Two");

        Assert.Equal(1, session.History.Count(m => m.Role == AgentRole.System));
        Assert.Equal(2, session.History.Count(m => m.Role == AgentRole.User));
        Assert.Equal(2, session.History.Count(m => m.Role == AgentRole.Assistant));
    }

    private sealed class ResetPlan
    {
        public string Title { get; set; } = string.Empty;

        public int Steps { get; set; }
    }
}

internal static class Phase1TestHost
{
    public static ServiceProvider Create(Action<PlexAgentOptions> configure, bool registerFake = true)
        => CreateServices(configure, registerFake).BuildServiceProvider();

    public static IServiceCollection CreateServices(Action<PlexAgentOptions> configure, bool registerFake = true)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));
        services.AddPlexAgent(configure);
        if (registerFake)
        {
            services.AddSingleton<ILlmProvider>(new FakeLlmProvider("Fake", "fake-1"));
        }

        return services;
    }
}

internal sealed class FakeLlmProvider : ILlmProvider
{
    private readonly string _defaultModel;
    private readonly string? _structuredJson;

    public FakeLlmProvider(string providerId, string defaultModel, string? structuredJson = null)
    {
        ProviderId = providerId;
        _defaultModel = defaultModel;
        _structuredJson = structuredJson;
    }

    public string ProviderId { get; }

    public ResponseFormatKind LastResponseFormat { get; private set; }

    public JsonSchemaResponseFormat? LastJsonSchema { get; private set; }

    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProviderCapabilities(
            SupportsToolCalling: false,
            SupportsStreaming: false,
            SupportsSystemMessages: true,
            SupportsJsonObject: true,
            SupportsJsonSchema: true,
            SupportedModels: [_defaultModel]));
    }

    public Task<ProviderCompletionResult> CompleteAsync(
        ProviderCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        LastResponseFormat = request.ResponseFormat;
        LastJsonSchema = request.JsonSchema;
        var lastUser = request.Messages.LastOrDefault(m => m.Role == AgentRole.User)?.Content ?? string.Empty;
        var content = _structuredJson ?? $"echo:{lastUser}";

        return Task.FromResult(new ProviderCompletionResult
        {
            Content = content,
            Model = request.Model,
            FinishReason = AgentFinishReason.Stop,
            Usage = new AgentUsage { InputTokens = 3, OutputTokens = 5, TotalTokens = 8 }
        });
    }
}
