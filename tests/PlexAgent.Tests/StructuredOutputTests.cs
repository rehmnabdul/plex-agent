using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlexAgent.Abstractions;
using PlexAgent.Configuration;
using PlexAgent.DependencyInjection;
using PlexAgent.Exceptions;
using PlexAgent.Models;
using PlexAgent.StructuredOutput;

namespace PlexAgent.Tests;

public class StructuredOutputTests
{
    [Fact]
    public void JsonSchemaGenerator_FromType_IncludesRequiredCamelCaseProperties()
    {
        var format = JsonSchemaGenerator.FromType<ResetPlan>();

        Assert.Equal("ResetPlan", format.Name);
        Assert.True(format.Strict);

        var root = format.Schema.RootElement;
        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        Assert.True(root.GetProperty("properties").TryGetProperty("title", out _));
        Assert.True(root.GetProperty("properties").TryGetProperty("steps", out _));
        Assert.True(root.GetProperty("properties").TryGetProperty("notes", out _));

        var required = root.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToHashSet();
        Assert.Contains("title", required);
        Assert.Contains("steps", required);
        Assert.DoesNotContain("notes", required);
    }

    [Fact]
    public async Task AskAsyncT_WhenProviderSupportsJsonSchema_SendsGeneratedSchema()
    {
        var fake = new StructuredFakeLlmProvider(
            supportsJsonObject: true,
            supportsJsonSchema: true,
            content: """{"title":"Reset","steps":2,"notes":null}""");

        using var provider = CreateHost(fake);
        var agent = provider.GetRequiredService<IAgentFactory>().GetAgent("Extractor");
        var response = await agent.AskAsync<ResetPlan>("extract");

        Assert.Equal("Reset", response.Data.Title);
        Assert.Equal(2, response.Data.Steps);
        Assert.Equal(ResponseFormatKind.JsonSchema, fake.LastResponseFormat);
        Assert.NotNull(fake.LastJsonSchema);
        Assert.Equal("ResetPlan", fake.LastJsonSchema!.Name);
        Assert.True(fake.LastJsonSchema.Schema.RootElement.GetProperty("properties").TryGetProperty("title", out _));
    }

    [Fact]
    public async Task AskAsyncT_WhenOnlyJsonObjectSupported_FallsBackAndValidatesRequired()
    {
        var fake = new StructuredFakeLlmProvider(
            supportsJsonObject: true,
            supportsJsonSchema: false,
            content: """{"title":"Reset","steps":3}""");

        using var provider = CreateHost(fake);
        var agent = provider.GetRequiredService<IAgentFactory>().GetAgent("Extractor");
        var response = await agent.AskAsync<ResetPlan>("extract");

        Assert.Equal(ResponseFormatKind.JsonObject, fake.LastResponseFormat);
        Assert.NotNull(fake.LastJsonSchema); // retained for post-validation
        Assert.Equal("Reset", response.Data.Title);
        Assert.Equal(3, response.Data.Steps);
    }

    [Fact]
    public async Task AskAsyncT_WhenRequiredPropertyMissing_ThrowsStructuredOutputException()
    {
        var fake = new StructuredFakeLlmProvider(
            supportsJsonObject: true,
            supportsJsonSchema: false,
            content: """{"steps":1}""");

        using var provider = CreateHost(fake);
        var agent = provider.GetRequiredService<IAgentFactory>().GetAgent("Extractor");

        var ex = await Assert.ThrowsAsync<StructuredOutputException>(() => agent.AskAsync<ResetPlan>("extract"));
        Assert.Contains("title", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("""{"steps":1}""", ex.RawContent);
    }

    [Fact]
    public async Task AskAsyncT_WhenStructuredOutputUnsupported_Throws()
    {
        var fake = new StructuredFakeLlmProvider(
            supportsJsonObject: false,
            supportsJsonSchema: false,
            content: "{}");

        using var provider = CreateHost(fake);
        var agent = provider.GetRequiredService<IAgentFactory>().GetAgent("Extractor");

        await Assert.ThrowsAsync<StructuredOutputNotSupportedException>(() => agent.AskAsync<ResetPlan>("extract"));
        Assert.Equal(0, fake.CallCount);
    }

    [Fact]
    public async Task AskAsyncT_WithExplicitSchema_UsesProvidedSchemaName()
    {
        var schema = JsonSchemaGenerator.FromType<ResetPlan>(name: "password_reset_plan");
        var fake = new StructuredFakeLlmProvider(
            supportsJsonObject: true,
            supportsJsonSchema: true,
            content: """{"title":"Reset","steps":1}""");

        using var provider = CreateHost(fake);
        var agent = provider.GetRequiredService<IAgentFactory>().GetAgent("Extractor");
        var response = await agent.AskAsync<ResetPlan>("extract", schema);

        Assert.Equal(ResponseFormatKind.JsonSchema, fake.LastResponseFormat);
        Assert.Equal("password_reset_plan", fake.LastJsonSchema!.Name);
        Assert.Equal("Reset", response.Data.Title);
    }

    private static ServiceProvider CreateHost(ILlmProvider llm)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));
        services.AddPlexAgent(options =>
        {
            options.Agents["Extractor"] = new AgentDefinitionOptions
            {
                Provider = llm.ProviderId,
                Model = "fake-1"
            };
        });
        services.AddSingleton(llm);
        return services.BuildServiceProvider();
    }

    private sealed class ResetPlan
    {
        public string Title { get; set; } = string.Empty;

        public int Steps { get; set; }

        public string? Notes { get; set; }
    }
}

internal sealed class StructuredFakeLlmProvider : ILlmProvider
{
    private readonly bool _supportsJsonObject;
    private readonly bool _supportsJsonSchema;
    private readonly string _content;

    public StructuredFakeLlmProvider(bool supportsJsonObject, bool supportsJsonSchema, string content)
    {
        _supportsJsonObject = supportsJsonObject;
        _supportsJsonSchema = supportsJsonSchema;
        _content = content;
        ProviderId = "Fake";
    }

    public string ProviderId { get; }

    public int CallCount { get; private set; }

    public ResponseFormatKind LastResponseFormat { get; private set; }

    public JsonSchemaResponseFormat? LastJsonSchema { get; private set; }

    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProviderCapabilities(
            SupportsToolCalling: false,
            SupportsStreaming: false,
            SupportsSystemMessages: true,
            SupportsJsonObject: _supportsJsonObject,
            SupportsJsonSchema: _supportsJsonSchema,
            SupportedModels: ["fake-1"]));
    }

    public Task<ProviderCompletionResult> CompleteAsync(
        ProviderCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastResponseFormat = request.ResponseFormat;
        LastJsonSchema = request.JsonSchema;
        return Task.FromResult(new ProviderCompletionResult
        {
            Content = _content,
            Model = request.Model,
            FinishReason = AgentFinishReason.Stop,
            Usage = new AgentUsage { InputTokens = 2, OutputTokens = 3, TotalTokens = 5 }
        });
    }
}
