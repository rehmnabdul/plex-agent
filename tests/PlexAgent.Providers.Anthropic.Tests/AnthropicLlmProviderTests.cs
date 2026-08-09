using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PlexAgent.Abstractions;
using PlexAgent.DependencyInjection;
using PlexAgent.Exceptions;
using PlexAgent.Models;

namespace PlexAgent.Providers.Anthropic.Tests;

public class AnthropicLlmProviderTests
{
    [Fact]
    public async Task CompleteAsync_WithMockedHttp_MapsUnifiedResponse()
    {
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """
            {
              "id": "msg_test",
              "type": "message",
              "role": "assistant",
              "model": "claude-3-5-haiku-latest",
              "content": [{ "type": "text", "text": "Hello from Anthropic mock" }],
              "stop_reason": "end_turn",
              "usage": { "input_tokens": 9, "output_tokens": 6 }
            }
            """);

        var provider = CreateProvider(handler, "sk-ant-test");
        var result = await provider.CompleteAsync(new ProviderCompletionRequest
        {
            Model = "claude-3-5-haiku-latest",
            Messages =
            [
                AgentMessage.System("Be brief."),
                AgentMessage.User("Hi")
            ],
            MaxTokens = 128
        });

        Assert.Equal("Hello from Anthropic mock", result.Content);
        Assert.Equal("claude-3-5-haiku-latest", result.Model);
        Assert.Equal(AgentFinishReason.Stop, result.FinishReason);
        Assert.Equal(9, result.Usage?.InputTokens);
        Assert.Equal(6, result.Usage?.OutputTokens);
        Assert.Equal(15, result.Usage?.TotalTokens);
        Assert.Contains("/v1/messages", handler.LastRequestUri?.AbsolutePath ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal("sk-ant-test", handler.LastApiKey);
    }

    [Fact]
    public async Task CompleteAsync_WhenApiKeyMissing_ThrowsProviderConfigurationException()
    {
        var handler = new QueueHttpMessageHandler();
        var provider = CreateProvider(handler, apiKey: "");

        var ex = await Assert.ThrowsAsync<ProviderConfigurationException>(() =>
            provider.CompleteAsync(new ProviderCompletionRequest
            {
                Model = "claude-3-5-haiku-latest",
                Messages = [AgentMessage.User("Hi")]
            }));

        Assert.Equal(LlmProviderIds.Anthropic, ex.ProviderId);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public void AddAnthropic_RegistersProviderInDi()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPlexAgent(_ => { }).AddAnthropic(o => o.ApiKey = "sk-ant-test");

        using var sp = services.BuildServiceProvider();
        var llm = Assert.Single(sp.GetServices<ILlmProvider>(), p => p.ProviderId == LlmProviderIds.Anthropic);
        Assert.Equal(LlmProviderIds.Anthropic, llm.ProviderId);
    }

    private static AnthropicLlmProvider CreateProvider(HttpMessageHandler handler, string apiKey)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<AnthropicOptions>>(Options.Create(new AnthropicOptions
        {
            ApiKey = apiKey,
            BaseUrl = AnthropicDefaults.DefaultBaseUrl
        }));
        services.AddHttpClient(AnthropicDefaults.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        var sp = services.BuildServiceProvider();
        return new AnthropicLlmProvider(
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<IOptions<AnthropicOptions>>());
    }
}

internal sealed class QueueHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    public Uri? LastRequestUri { get; private set; }

    public string? LastApiKey { get; private set; }

    public int RequestCount { get; private set; }

    public void EnqueueJson(HttpStatusCode statusCode, string json)
    {
        _responses.Enqueue(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        LastRequestUri = request.RequestUri;
        LastApiKey = request.Headers.TryGetValues("x-api-key", out var values) ? values.FirstOrDefault() : null;
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("No queued HTTP response.");
        }

        return Task.FromResult(_responses.Dequeue());
    }
}
