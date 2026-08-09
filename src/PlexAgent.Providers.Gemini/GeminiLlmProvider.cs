using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PlexAgent.Abstractions;
using PlexAgent.Exceptions;
using PlexAgent.Models;

namespace PlexAgent.Providers.Gemini;

internal sealed class GeminiLlmProvider : ILlmProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<GeminiOptions> _options;

    public GeminiLlmProvider(IHttpClientFactory httpClientFactory, IOptions<GeminiOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public string ProviderId => LlmProviderIds.Gemini;

    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProviderCapabilities(
            SupportsToolCalling: true,
            SupportsStreaming: true,
            SupportsSystemMessages: true,
            SupportsJsonObject: true,
            SupportsJsonSchema: true,
            SupportedModels: Array.Empty<string>()));
    }

    public async Task<ProviderCompletionResult> CompleteAsync(
        ProviderCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var options = _options.Value;
        EnsureApiKey(options);

        var client = _httpClientFactory.CreateClient(GeminiDefaults.HttpClientName);
        using var httpRequest = CreateRequest(request, options, stream: false);
        using var response = await client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new ProviderRequestException(
                LlmProviderIds.Gemini,
                $"Gemini request failed with {(int)response.StatusCode}: {body}");
        }

        return MapResponse(body, request.Model);
    }

    public async IAsyncEnumerable<ProviderStreamUpdate> StreamAsync(
        ProviderCompletionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var options = _options.Value;
        EnsureApiKey(options);

        var client = _httpClientFactory.CreateClient(GeminiDefaults.HttpClientName);
        using var httpRequest = CreateRequest(request, options, stream: true);
        using var response = await client
            .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        var bodyStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            using var reader = new StreamReader(bodyStream);
            var errorBody = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            throw new ProviderRequestException(
                LlmProviderIds.Gemini,
                $"Gemini stream failed with {(int)response.StatusCode}: {errorBody}");
        }

        var contentBuilder = new StringBuilder();
        List<ToolCall>? toolCalls = null;
        AgentFinishReason finishReason = AgentFinishReason.Stop;
        AgentUsage? usage = null;

        using var streamReader = new StreamReader(bodyStream);
        while (!streamReader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await streamReader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line["data:".Length..].Trim();
            if (data.Length == 0 || data == "[DONE]")
            {
                continue;
            }

            using var document = JsonDocument.Parse(data);
            var mapped = MapResponse(document.RootElement.GetRawText(), request.Model);

            if (!string.IsNullOrEmpty(mapped.Content))
            {
                // Gemini stream chunks are cumulative per event in some modes; prefer delta by suffix.
                var incoming = mapped.Content;
                if (incoming.StartsWith(contentBuilder.ToString(), StringComparison.Ordinal))
                {
                    var delta = incoming[contentBuilder.Length..];
                    if (!string.IsNullOrEmpty(delta))
                    {
                        contentBuilder.Append(delta);
                        yield return new ProviderStreamUpdate { TextDelta = delta };
                    }
                }
                else
                {
                    contentBuilder.Append(incoming);
                    yield return new ProviderStreamUpdate { TextDelta = incoming };
                }
            }

            if (mapped.ToolCalls is { Count: > 0 })
            {
                toolCalls = mapped.ToolCalls
                    .Select((call, index) => new ToolCall
                    {
                        Id = string.IsNullOrWhiteSpace(call.Id) ? $"call_{index}" : call.Id,
                        Name = call.Name,
                        ArgumentsJson = call.ArgumentsJson
                    })
                    .ToList();
            }

            if (mapped.Usage is not null)
            {
                usage = mapped.Usage;
            }

            finishReason = mapped.FinishReason;
        }

        yield return new ProviderStreamUpdate
        {
            Completed = new ProviderCompletionResult
            {
                Content = contentBuilder.ToString(),
                ToolCalls = toolCalls,
                Model = request.Model,
                FinishReason = toolCalls is { Count: > 0 } ? AgentFinishReason.ToolCalls : finishReason,
                Usage = usage
            }
        };
    }

    private static void EnsureApiKey(GeminiOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new ProviderConfigurationException(
                LlmProviderIds.Gemini,
                "Gemini API key is missing. Set PlexAgent:Providers:Gemini:ApiKey or configure AddGemini options.");
        }
    }

    private static HttpRequestMessage CreateRequest(ProviderCompletionRequest request, GeminiOptions options, bool stream)
    {
        var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl)
            ? GeminiDefaults.DefaultBaseUrl
            : options.BaseUrl.TrimEnd('/');

        var model = Uri.EscapeDataString(request.Model);
        var method = stream ? "streamGenerateContent" : "generateContent";
        var url = $"{baseUrl}/v1beta/models/{model}:{method}?key={Uri.EscapeDataString(options.ApiKey)}";
        if (stream)
        {
            url += "&alt=sse";
        }

        var payload = BuildPayload(request);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload.ToJsonString(SerializerOptions), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(stream ? "text/event-stream" : "application/json"));
        return httpRequest;
    }

    private static JsonObject BuildPayload(ProviderCompletionRequest request)
    {
        var (systemPrompt, contents) = SplitMessages(request.Messages);
        var payload = new JsonObject
        {
            ["contents"] = contents
        };

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            payload["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray(new JsonObject { ["text"] = systemPrompt })
            };
        }

        var generationConfig = new JsonObject();
        if (request.Temperature is float temperature)
        {
            generationConfig["temperature"] = temperature;
        }

        if (request.MaxTokens is int maxTokens)
        {
            generationConfig["maxOutputTokens"] = maxTokens;
        }

        if (request.TopP is float topP)
        {
            generationConfig["topP"] = topP;
        }

        if (request.StopSequences is { Count: > 0 })
        {
            generationConfig["stopSequences"] = new JsonArray(request.StopSequences.Select(s => JsonValue.Create(s)).ToArray());
        }

        if (request.ResponseFormat == ResponseFormatKind.JsonObject)
        {
            generationConfig["responseMimeType"] = "application/json";
        }
        else if (request.ResponseFormat == ResponseFormatKind.JsonSchema && request.JsonSchema is not null)
        {
            generationConfig["responseMimeType"] = "application/json";
            generationConfig["responseSchema"] = JsonNode.Parse(request.JsonSchema.Schema.RootElement.GetRawText());
        }

        if (generationConfig.Count > 0)
        {
            payload["generationConfig"] = generationConfig;
        }

        if (request.Tools is { Count: > 0 })
        {
            var declarations = new JsonArray();
            foreach (var tool in request.Tools)
            {
                declarations.Add(new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = JsonNode.Parse(tool.ParameterSchema.RootElement.GetRawText())
                });
            }

            payload["tools"] = new JsonArray(new JsonObject
            {
                ["functionDeclarations"] = declarations
            });
        }

        return payload;
    }

    private static (string? SystemPrompt, JsonArray Contents) SplitMessages(IReadOnlyList<AgentMessage> messages)
    {
        string? system = null;
        var contents = new JsonArray();

        foreach (var message in messages)
        {
            if (message.Role == AgentRole.System)
            {
                system = string.IsNullOrWhiteSpace(system)
                    ? message.Content
                    : $"{system}\n{message.Content}";
                continue;
            }

            if (message.Role == AgentRole.Tool)
            {
                contents.Add(new JsonObject
                {
                    ["role"] = "user",
                    ["parts"] = new JsonArray(new JsonObject
                    {
                        ["functionResponse"] = new JsonObject
                        {
                            ["name"] = message.Name ?? "tool",
                            ["response"] = new JsonObject
                            {
                                ["result"] = message.Content
                            }
                        }
                    })
                });
                continue;
            }

            if (message.Role == AgentRole.Assistant && message.ToolCalls is { Count: > 0 })
            {
                var parts = new JsonArray();
                if (!string.IsNullOrWhiteSpace(message.Content))
                {
                    parts.Add(new JsonObject { ["text"] = message.Content });
                }

                foreach (var call in message.ToolCalls)
                {
                    parts.Add(new JsonObject
                    {
                        ["functionCall"] = new JsonObject
                        {
                            ["name"] = call.Name,
                            ["args"] = JsonNode.Parse(string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson)
                        }
                    });
                }

                contents.Add(new JsonObject { ["role"] = "model", ["parts"] = parts });
                continue;
            }

            contents.Add(new JsonObject
            {
                ["role"] = message.Role == AgentRole.Assistant ? "model" : "user",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = message.Content })
            });
        }

        return (system, contents);
    }

    private static ProviderCompletionResult MapResponse(string body, string fallbackModel)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var contentBuilder = new StringBuilder();
        List<ToolCall>? toolCalls = null;
        var finishReason = AgentFinishReason.Stop;

        if (root.TryGetProperty("candidates", out var candidates) &&
            candidates.ValueKind == JsonValueKind.Array &&
            candidates.GetArrayLength() > 0)
        {
            var candidate = candidates[0];
            if (candidate.TryGetProperty("finishReason", out var finish))
            {
                finishReason = MapFinishReason(finish.GetString());
            }

            if (candidate.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) &&
                parts.ValueKind == JsonValueKind.Array)
            {
                var toolIndex = 0;
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var text))
                    {
                        contentBuilder.Append(text.GetString());
                    }
                    else if (part.TryGetProperty("functionCall", out var functionCall))
                    {
                        toolCalls ??= [];
                        toolCalls.Add(new ToolCall
                        {
                            Id = $"call_{toolIndex++}",
                            Name = functionCall.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
                            ArgumentsJson = functionCall.TryGetProperty("args", out var args) ? args.GetRawText() : "{}"
                        });
                    }
                }
            }
        }

        AgentUsage? usage = null;
        if (root.TryGetProperty("usageMetadata", out var usageElement))
        {
            var input = usageElement.TryGetProperty("promptTokenCount", out var prompt) ? prompt.GetInt32() : (int?)null;
            var output = usageElement.TryGetProperty("candidatesTokenCount", out var candidatesTokens)
                ? candidatesTokens.GetInt32()
                : (int?)null;
            int? total = usageElement.TryGetProperty("totalTokenCount", out var totalTokens)
                ? totalTokens.GetInt32()
                : input is int i && output is int o ? i + o : null;

            usage = new AgentUsage
            {
                InputTokens = input,
                OutputTokens = output,
                TotalTokens = total
            };
        }

        return new ProviderCompletionResult
        {
            Content = contentBuilder.ToString(),
            ToolCalls = toolCalls,
            Model = fallbackModel,
            FinishReason = toolCalls is { Count: > 0 } ? AgentFinishReason.ToolCalls : finishReason,
            Usage = usage
        };
    }

    private static AgentFinishReason MapFinishReason(string? finishReason) =>
        finishReason switch
        {
            "STOP" => AgentFinishReason.Stop,
            "MAX_TOKENS" => AgentFinishReason.Length,
            "SAFETY" or "RECITATION" => AgentFinishReason.ContentFilter,
            _ => AgentFinishReason.Unknown
        };
}
