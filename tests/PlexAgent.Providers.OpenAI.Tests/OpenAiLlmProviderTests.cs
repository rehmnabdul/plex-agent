using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PlexAgent.Abstractions;
using PlexAgent.DependencyInjection;
using PlexAgent.Exceptions;
using PlexAgent.Models;

namespace PlexAgent.Providers.OpenAI.Tests;

public class OpenAiLlmProviderTests
{
    [Fact]
    public async Task CompleteAsync_WithMockedHttp_MapsUnifiedResponse()
    {
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """
            {
              "id": "chatcmpl-test",
              "object": "chat.completion",
              "created": 1710000000,
              "model": "gpt-4o-mini",
              "choices": [
                {
                  "index": 0,
                  "message": { "role": "assistant", "content": "Hello from OpenAI mock" },
                  "finish_reason": "stop"
                }
              ],
              "usage": {
                "prompt_tokens": 11,
                "completion_tokens": 7,
                "total_tokens": 18
              }
            }
            """);

        var provider = CreateProvider(handler, apiKey: "sk-test");
        var result = await provider.CompleteAsync(new ProviderCompletionRequest
        {
            Model = "gpt-4o-mini",
            Messages =
            [
                AgentMessage.System("You are helpful."),
                AgentMessage.User("Hi")
            ],
            Temperature = 0.2f,
            MaxTokens = 64
        });

        Assert.Equal("Hello from OpenAI mock", result.Content);
        Assert.Equal("gpt-4o-mini", result.Model);
        Assert.Equal(AgentFinishReason.Stop, result.FinishReason);
        Assert.Equal(11, result.Usage?.InputTokens);
        Assert.Equal(7, result.Usage?.OutputTokens);
        Assert.Equal(18, result.Usage?.TotalTokens);
        Assert.Contains("/chat/completions", handler.LastRequestUri?.AbsolutePath ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompleteAsync_WhenApiKeyMissing_ThrowsProviderConfigurationException()
    {
        var handler = new QueueHttpMessageHandler();
        var provider = CreateProvider(handler, apiKey: "");

        var ex = await Assert.ThrowsAsync<ProviderConfigurationException>(() =>
            provider.CompleteAsync(new ProviderCompletionRequest
            {
                Model = "gpt-4o-mini",
                Messages = [AgentMessage.User("Hi")]
            }));

        Assert.Equal(LlmProviderIds.OpenAI, ex.ProviderId);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public void AddOpenAI_RegistersProviderInDi()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPlexAgent(options =>
        {
            options.Agents["Demo"] = new Configuration.AgentDefinitionOptions
            {
                Provider = LlmProviderIds.OpenAI,
                Model = "gpt-4o-mini"
            };
        }).AddOpenAI(o =>
        {
            o.ApiKey = "sk-test";
            o.DefaultModel = "gpt-4o-mini";
        });

        using var sp = services.BuildServiceProvider();
        var llm = Assert.Single(sp.GetServices<ILlmProvider>(), p => p.ProviderId == LlmProviderIds.OpenAI);
        Assert.Equal(LlmProviderIds.OpenAI, llm.ProviderId);
    }

    [Fact]
    public void ProviderId_IsOpenAI()
    {
        Assert.Equal("OpenAI", LlmProviderIds.OpenAI);
        Assert.Equal("PlexAgent.OpenAI", OpenAiDefaults.HttpClientName);
    }

    private static OpenAiLlmProvider CreateProvider(HttpMessageHandler handler, string apiKey)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<OpenAiOptions>>(Options.Create(new OpenAiOptions
        {
            ApiKey = apiKey,
            BaseUrl = OpenAiDefaults.DefaultBaseUrl,
            TimeoutSeconds = 30
        }));
        services.AddHttpClient(OpenAiDefaults.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        var sp = services.BuildServiceProvider();
        return new OpenAiLlmProvider(
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<IOptions<OpenAiOptions>>());
    }
}

internal sealed class QueueHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    public Uri? LastRequestUri { get; private set; }

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
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("No queued HTTP response.");
        }

        return Task.FromResult(_responses.Dequeue());
    }
}
