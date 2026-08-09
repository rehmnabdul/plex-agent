using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PlexAgent.Abstractions;
using PlexAgent.Exceptions;
using PlexAgent.Models;

namespace PlexAgent.Providers.Anthropic;

internal sealed class AnthropicLlmProvider : ILlmProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<AnthropicOptions> _options;

    public AnthropicLlmProvider(IHttpClientFactory httpClientFactory, IOptions<AnthropicOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public string ProviderId => LlmProviderIds.Anthropic;

    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProviderCapabilities(
            SupportsToolCalling: true,
            SupportsStreaming: true,
            SupportsSystemMessages: true,
            SupportsJsonObject: true,
            SupportsJsonSchema: false,
            SupportedModels: Array.Empty<string>()));
    }

    public async Task<ProviderCompletionResult> CompleteAsync(
        ProviderCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var options = _options.Value;
        EnsureApiKey(options);

        var client = _httpClientFactory.CreateClient(AnthropicDefaults.HttpClientName);
        using var httpRequest = CreateRequest(request, options, stream: false);
        using var response = await client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new ProviderRequestException(
                LlmProviderIds.Anthropic,
                $"Anthropic request failed with {(int)response.StatusCode}: {body}");
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

        var client = _httpClientFactory.CreateClient(AnthropicDefaults.HttpClientName);
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
                LlmProviderIds.Anthropic,
                $"Anthropic stream failed with {(int)response.StatusCode}: {errorBody}");
        }

        var contentBuilder = new StringBuilder();
        var toolBuilders = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();
        AgentFinishReason finishReason = AgentFinishReason.Stop;
        string model = request.Model;
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
            var root = document.RootElement;
            var type = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;

            if (type == "content_block_delta" &&
                root.TryGetProperty("delta", out var delta) &&
                delta.TryGetProperty("type", out var deltaType))
            {
                var deltaKind = deltaType.GetString();
                if (deltaKind == "text_delta" && delta.TryGetProperty("text", out var text))
                {
                    var chunk = text.GetString();
                    if (!string.IsNullOrEmpty(chunk))
                    {
                        contentBuilder.Append(chunk);
                        yield return new ProviderStreamUpdate { TextDelta = chunk };
                    }
                }
                else if (deltaKind == "input_json_delta" &&
                         delta.TryGetProperty("partial_json", out var partial) &&
                         root.TryGetProperty("index", out var indexProp))
                {
                    var index = indexProp.GetInt32();
                    if (!toolBuilders.TryGetValue(index, out var builder))
                    {
                        builder = (string.Empty, string.Empty, new StringBuilder());
                    }

                    builder.Args.Append(partial.GetString());
                    toolBuilders[index] = builder;
                }
            }
            else if (type == "content_block_start" &&
                     root.TryGetProperty("content_block", out var block) &&
                     root.TryGetProperty("index", out var startIndex) &&
                     block.TryGetProperty("type", out var blockType) &&
                     blockType.GetString() == "tool_use")
            {
                toolBuilders[startIndex.GetInt32()] = (
                    block.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                    block.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
                    new StringBuilder());
            }
            else if (type == "message_delta")
            {
                if (root.TryGetProperty("delta", out var messageDelta) &&
                    messageDelta.TryGetProperty("stop_reason", out var stopReason))
                {
                    finishReason = MapFinishReason(stopReason.GetString());
                }

                if (root.TryGetProperty("usage", out var usageElement))
                {
                    int? output = usageElement.TryGetProperty("output_tokens", out var outputProp)
                        ? outputProp.GetInt32()
                        : null;
                    usage = new AgentUsage
                    {
                        OutputTokens = output,
                        TotalTokens = output
                    };
                }
            }
            else if (type == "message_start" &&
                     root.TryGetProperty("message", out var message) &&
                     message.TryGetProperty("model", out var modelProp))
            {
                model = modelProp.GetString() ?? model;
                if (message.TryGetProperty("usage", out var startUsage))
                {
                    int? input = startUsage.TryGetProperty("input_tokens", out var inputProp)
                        ? inputProp.GetInt32()
                        : null;
                    usage = new AgentUsage
                    {
                        InputTokens = input,
                        OutputTokens = usage?.OutputTokens,
                        TotalTokens = input is int i && usage?.OutputTokens is int o ? i + o : input
                    };
                }
            }
        }

        IReadOnlyList<ToolCall>? toolCalls = null;
        if (toolBuilders.Count > 0)
        {
            toolCalls = toolBuilders
                .OrderBy(static pair => pair.Key)
                .Select(static pair => new ToolCall
                {
                    Id = pair.Value.Id,
                    Name = pair.Value.Name,
                    ArgumentsJson = pair.Value.Args.Length == 0 ? "{}" : pair.Value.Args.ToString()
                })
                .ToArray();
            if (finishReason == AgentFinishReason.Stop)
            {
                finishReason = AgentFinishReason.ToolCalls;
            }
        }

        yield return new ProviderStreamUpdate
        {
            Completed = new ProviderCompletionResult
            {
                Content = contentBuilder.ToString(),
                ToolCalls = toolCalls,
                Model = model,
                FinishReason = finishReason,
                Usage = usage
            }
        };
    }

    private static void EnsureApiKey(AnthropicOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new ProviderConfigurationException(
                LlmProviderIds.Anthropic,
                "Anthropic API key is missing. Set PlexAgent:Providers:Anthropic:ApiKey or configure AddAnthropic options.");
        }
    }

    private static HttpRequestMessage CreateRequest(ProviderCompletionRequest request, AnthropicOptions options, bool stream)
    {
        var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl)
            ? AnthropicDefaults.DefaultBaseUrl
            : options.BaseUrl.TrimEnd('/');

        var payload = BuildPayload(request);
        if (stream)
        {
            payload["stream"] = true;
        }

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/messages")
        {
            Content = new StringContent(payload.ToJsonString(SerializerOptions), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Add("x-api-key", options.ApiKey);
        httpRequest.Headers.Add("anthropic-version", AnthropicDefaults.ApiVersion);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(stream ? "text/event-stream" : "application/json"));
        return httpRequest;
    }

    private static JsonObject BuildPayload(ProviderCompletionRequest request)
    {
        var (systemPrompt, messages) = SplitMessages(request.Messages);
        var payload = new JsonObject
        {
            ["model"] = request.Model,
            ["max_tokens"] = request.MaxTokens ?? 1024,
            ["messages"] = messages
        };

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            payload["system"] = systemPrompt;
        }

        if (request.Temperature is float temperature)
        {
            payload["temperature"] = temperature;
        }

        if (request.TopP is float topP)
        {
            payload["top_p"] = topP;
        }

        if (request.StopSequences is { Count: > 0 })
        {
            payload["stop_sequences"] = new JsonArray(request.StopSequences.Select(s => JsonValue.Create(s)).ToArray());
        }

        if (request.ResponseFormat is ResponseFormatKind.JsonObject or ResponseFormatKind.JsonSchema)
        {
            // Anthropic has no native json_object mode in Messages API; request JSON via system guidance.
            var guidance = "Respond with valid JSON only.";
            payload["system"] = string.IsNullOrWhiteSpace(systemPrompt)
                ? guidance
                : $"{systemPrompt}\n\n{guidance}";
        }

        if (request.Tools is { Count: > 0 })
        {
            var tools = new JsonArray();
            foreach (var tool in request.Tools)
            {
                tools.Add(new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["input_schema"] = JsonNode.Parse(tool.ParameterSchema.RootElement.GetRawText())
                });
            }

            payload["tools"] = tools;
        }

        return payload;
    }

    private static (string? SystemPrompt, JsonArray Messages) SplitMessages(IReadOnlyList<AgentMessage> messages)
    {
        string? system = null;
        var mapped = new JsonArray();

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
                mapped.Add(new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = new JsonArray(new JsonObject
                    {
                        ["type"] = "tool_result",
                        ["tool_use_id"] = message.ToolCallId ?? message.Name ?? "tool",
                        ["content"] = message.Content
                    })
                });
                continue;
            }

            if (message.Role == AgentRole.Assistant && message.ToolCalls is { Count: > 0 })
            {
                var content = new JsonArray();
                if (!string.IsNullOrWhiteSpace(message.Content))
                {
                    content.Add(new JsonObject { ["type"] = "text", ["text"] = message.Content });
                }

                foreach (var call in message.ToolCalls)
                {
                    content.Add(new JsonObject
                    {
                        ["type"] = "tool_use",
                        ["id"] = call.Id,
                        ["name"] = call.Name,
                        ["input"] = JsonNode.Parse(string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson)
                    });
                }

                mapped.Add(new JsonObject { ["role"] = "assistant", ["content"] = content });
                continue;
            }

            mapped.Add(new JsonObject
            {
                ["role"] = message.Role == AgentRole.Assistant ? "assistant" : "user",
                ["content"] = message.Content
            });
        }

        return (system, mapped);
    }

    private static ProviderCompletionResult MapResponse(string body, string fallbackModel)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var contentBuilder = new StringBuilder();
        List<ToolCall>? toolCalls = null;

        if (root.TryGetProperty("content", out var contentArray) && contentArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in contentArray.EnumerateArray())
            {
                var type = part.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;
                if (type == "text" && part.TryGetProperty("text", out var text))
                {
                    contentBuilder.Append(text.GetString());
                }
                else if (type == "tool_use")
                {
                    toolCalls ??= [];
                    toolCalls.Add(new ToolCall
                    {
                        Id = part.GetProperty("id").GetString() ?? string.Empty,
                        Name = part.GetProperty("name").GetString() ?? string.Empty,
                        ArgumentsJson = part.TryGetProperty("input", out var input)
                            ? input.GetRawText()
                            : "{}"
                    });
                }
            }
        }

        AgentUsage? usage = null;
        if (root.TryGetProperty("usage", out var usageElement))
        {
            int? input = usageElement.TryGetProperty("input_tokens", out var inputProp) ? inputProp.GetInt32() : null;
            int? output = usageElement.TryGetProperty("output_tokens", out var outputProp) ? outputProp.GetInt32() : null;
            usage = new AgentUsage
            {
                InputTokens = input,
                OutputTokens = output,
                TotalTokens = input is int i && output is int o ? i + o : null
            };
        }

        var stopReason = root.TryGetProperty("stop_reason", out var stop) ? stop.GetString() : null;
        var model = root.TryGetProperty("model", out var modelProp) ? modelProp.GetString() : fallbackModel;

        return new ProviderCompletionResult
        {
            Content = contentBuilder.ToString(),
            ToolCalls = toolCalls,
            Model = model ?? fallbackModel,
            FinishReason = MapFinishReason(stopReason),
            Usage = usage
        };
    }

    private static AgentFinishReason MapFinishReason(string? stopReason) =>
        stopReason switch
        {
            "end_turn" or "stop_sequence" => AgentFinishReason.Stop,
            "max_tokens" => AgentFinishReason.Length,
            "tool_use" => AgentFinishReason.ToolCalls,
            _ => AgentFinishReason.Unknown
        };
}
