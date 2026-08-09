using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PlexAgent.Abstractions;
using PlexAgent.DependencyInjection;
using PlexAgent.Exceptions;
using PlexAgent.Models;

namespace PlexAgent.Providers.Gemini.Tests;

public class GeminiLlmProviderTests
{
    [Fact]
    public async Task CompleteAsync_WithMockedHttp_MapsUnifiedResponse()
    {
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """
            {
              "candidates": [
                {
                  "content": {
                    "role": "model",
                    "parts": [{ "text": "Hello from Gemini mock" }]
                  },
                  "finishReason": "STOP"
                }
              ],
              "usageMetadata": {
                "promptTokenCount": 8,
                "candidatesTokenCount": 5,
                "totalTokenCount": 13
              }
            }
            """);

        var provider = CreateProvider(handler, "gemini-key");
        var result = await provider.CompleteAsync(new ProviderCompletionRequest
        {
            Model = "gemini-2.0-flash",
            Messages =
            [
                AgentMessage.System("Be brief."),
                AgentMessage.User("Hi")
            ],
            Temperature = 0.1f,
            ResponseFormat = ResponseFormatKind.JsonObject
        });

        Assert.Equal("Hello from Gemini mock", result.Content);
        Assert.Equal("gemini-2.0-flash", result.Model);
        Assert.Equal(AgentFinishReason.Stop, result.FinishReason);
        Assert.Equal(8, result.Usage?.InputTokens);
        Assert.Equal(5, result.Usage?.OutputTokens);
        Assert.Equal(13, result.Usage?.TotalTokens);
        Assert.Contains("gemini-2.0-flash:generateContent", handler.LastRequestUri?.AbsolutePath ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("key=gemini-key", handler.LastRequestUri?.Query ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteAsync_WhenApiKeyMissing_ThrowsProviderConfigurationException()
    {
        var handler = new QueueHttpMessageHandler();
        var provider = CreateProvider(handler, apiKey: "");

        var ex = await Assert.ThrowsAsync<ProviderConfigurationException>(() =>
            provider.CompleteAsync(new ProviderCompletionRequest
            {
                Model = "gemini-2.0-flash",
                Messages = [AgentMessage.User("Hi")]
            }));

        Assert.Equal(LlmProviderIds.Gemini, ex.ProviderId);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public void AddGemini_RegistersProviderInDi()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPlexAgent(_ => { }).AddGemini(o => o.ApiKey = "gemini-key");

        using var sp = services.BuildServiceProvider();
        var llm = Assert.Single(sp.GetServices<ILlmProvider>(), p => p.ProviderId == LlmProviderIds.Gemini);
        Assert.Equal(LlmProviderIds.Gemini, llm.ProviderId);
    }

    private static GeminiLlmProvider CreateProvider(HttpMessageHandler handler, string apiKey)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<GeminiOptions>>(Options.Create(new GeminiOptions
        {
            ApiKey = apiKey,
            BaseUrl = GeminiDefaults.DefaultBaseUrl
        }));
        services.AddHttpClient(GeminiDefaults.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        var sp = services.BuildServiceProvider();
        return new GeminiLlmProvider(
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<IOptions<GeminiOptions>>());
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
