using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using PlexAgent.Abstractions;
using PlexAgent.Exceptions;
using PlexAgent.Models;

namespace PlexAgent.Providers.OpenAI;

internal sealed class OpenAiLlmProvider : ILlmProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<OpenAiOptions> _options;

    public OpenAiLlmProvider(IHttpClientFactory httpClientFactory, IOptions<OpenAiOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public string ProviderId => LlmProviderIds.OpenAI;

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

        var client = CreateClient();
        var chatClient = client.GetChatClient(request.Model);
        var messages = MapMessages(request.Messages);
        var options = MapOptions(request);

        ClientResult<ChatCompletion> result = await chatClient
            .CompleteChatAsync(messages, options, cancellationToken)
            .ConfigureAwait(false);

        ChatCompletion completion = result.Value;
        return MapResult(completion);
    }

    public async IAsyncEnumerable<ProviderStreamUpdate> StreamAsync(
        ProviderCompletionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var client = CreateClient();
        var chatClient = client.GetChatClient(request.Model);
        var messages = MapMessages(request.Messages);
        var options = MapOptions(request);

        var contentBuilder = new StringBuilder();
        var toolBuilders = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();
        AgentFinishReason finishReason = AgentFinishReason.Stop;
        string model = request.Model;
        AgentUsage? usage = null;

        await foreach (var update in chatClient.CompleteChatStreamingAsync(messages, options, cancellationToken)
            .ConfigureAwait(false))
        {
            if (!string.IsNullOrEmpty(update.Model))
            {
                model = update.Model;
            }

            if (update.Usage is not null)
            {
                usage = new AgentUsage
                {
                    InputTokens = update.Usage.InputTokenCount,
                    OutputTokens = update.Usage.OutputTokenCount,
                    TotalTokens = update.Usage.TotalTokenCount
                };
            }

            if (update.FinishReason is ChatFinishReason chatFinish)
            {
                finishReason = MapFinishReason(chatFinish);
            }

            if (update.ContentUpdate is { Count: > 0 })
            {
                var delta = ExtractText(update.ContentUpdate);
                if (!string.IsNullOrEmpty(delta))
                {
                    contentBuilder.Append(delta);
                    yield return new ProviderStreamUpdate { TextDelta = delta };
                }
            }

            if (update.ToolCallUpdates is { Count: > 0 })
            {
                foreach (var toolUpdate in update.ToolCallUpdates)
                {
                    if (!toolBuilders.TryGetValue(toolUpdate.Index, out var builder))
                    {
                        builder = (
                            toolUpdate.ToolCallId ?? string.Empty,
                            toolUpdate.FunctionName ?? string.Empty,
                            new StringBuilder());
                    }

                    if (!string.IsNullOrEmpty(toolUpdate.ToolCallId))
                    {
                        builder.Id = toolUpdate.ToolCallId;
                    }

                    if (!string.IsNullOrEmpty(toolUpdate.FunctionName))
                    {
                        builder.Name = toolUpdate.FunctionName;
                    }

                    if (toolUpdate.FunctionArgumentsUpdate is not null)
                    {
                        builder.Args.Append(toolUpdate.FunctionArgumentsUpdate);
                    }

                    toolBuilders[toolUpdate.Index] = builder;
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

    private OpenAIClient CreateClient()
    {
        var options = _options.Value;
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new ProviderConfigurationException(
                LlmProviderIds.OpenAI,
                "OpenAI API key is missing. Set PlexAgent:Providers:OpenAI:ApiKey or configure AddOpenAI options.");
        }

        var httpClient = _httpClientFactory.CreateClient(OpenAiDefaults.HttpClientName);
        var clientOptions = new OpenAIClientOptions
        {
            Transport = new HttpClientPipelineTransport(httpClient),
            OrganizationId = string.IsNullOrWhiteSpace(options.OrganizationId) ? null : options.OrganizationId
        };

        var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl)
            ? OpenAiDefaults.DefaultBaseUrl
            : options.BaseUrl;
        clientOptions.Endpoint = new Uri(baseUrl);

        if (options.TimeoutSeconds > 0)
        {
            clientOptions.NetworkTimeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        }

        return new OpenAIClient(new ApiKeyCredential(options.ApiKey), clientOptions);
    }

    private static List<ChatMessage> MapMessages(IReadOnlyList<AgentMessage> messages)
    {
        var mapped = new List<ChatMessage>(messages.Count);
        foreach (var message in messages)
        {
            mapped.Add(message.Role switch
            {
                AgentRole.System => new SystemChatMessage(message.Content),
                AgentRole.User => new UserChatMessage(message.Content),
                AgentRole.Assistant when message.ToolCalls is { Count: > 0 } =>
                    new AssistantChatMessage(MapToolCalls(message.ToolCalls)),
                AgentRole.Assistant => new AssistantChatMessage(message.Content),
                AgentRole.Tool => new ToolChatMessage(
                    message.ToolCallId ?? message.Name ?? "tool",
                    message.Content),
                _ => new UserChatMessage(message.Content)
            });
        }

        return mapped;
    }

    private static IEnumerable<ChatToolCall> MapToolCalls(IReadOnlyList<ToolCall> toolCalls)
    {
        foreach (var call in toolCalls)
        {
            yield return ChatToolCall.CreateFunctionToolCall(
                call.Id,
                call.Name,
                BinaryData.FromString(call.ArgumentsJson));
        }
    }

    private static ChatCompletionOptions MapOptions(ProviderCompletionRequest request)
    {
        var options = new ChatCompletionOptions();

        if (request.Temperature is float temperature)
        {
            options.Temperature = temperature;
        }

        if (request.MaxTokens is int maxTokens)
        {
            options.MaxOutputTokenCount = maxTokens;
        }

        if (request.TopP is float topP)
        {
            options.TopP = topP;
        }

        if (request.StopSequences is { Count: > 0 })
        {
            foreach (var stop in request.StopSequences)
            {
                options.StopSequences.Add(stop);
            }
        }

        options.ResponseFormat = request.ResponseFormat switch
        {
            ResponseFormatKind.JsonObject => ChatResponseFormat.CreateJsonObjectFormat(),
            ResponseFormatKind.JsonSchema when request.JsonSchema is not null =>
                ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: request.JsonSchema.Name,
                    jsonSchema: BinaryData.FromString(request.JsonSchema.Schema.RootElement.GetRawText()),
                    jsonSchemaFormatDescription: request.JsonSchema.Description,
                    jsonSchemaIsStrict: request.JsonSchema.Strict),
            _ => ChatResponseFormat.CreateTextFormat()
        };

        if (request.Tools is { Count: > 0 })
        {
            foreach (var tool in request.Tools)
            {
                options.Tools.Add(ChatTool.CreateFunctionTool(
                    functionName: tool.Name,
                    functionDescription: tool.Description,
                    functionParameters: BinaryData.FromString(tool.ParameterSchema.RootElement.GetRawText())));
            }
        }

        return options;
    }

    private static ProviderCompletionResult MapResult(ChatCompletion completion)
    {
        var content = ExtractText(completion.Content);
        IReadOnlyList<ToolCall>? toolCalls = null;
        if (completion.ToolCalls is { Count: > 0 })
        {
            toolCalls = completion.ToolCalls
                .Select(static call => new ToolCall
                {
                    Id = call.Id,
                    Name = call.FunctionName,
                    ArgumentsJson = call.FunctionArguments.ToString()
                })
                .ToArray();
        }

        return new ProviderCompletionResult
        {
            Content = content,
            ToolCalls = toolCalls,
            Model = completion.Model,
            FinishReason = MapFinishReason(completion.FinishReason),
            Usage = completion.Usage is null
                ? null
                : new AgentUsage
                {
                    InputTokens = completion.Usage.InputTokenCount,
                    OutputTokens = completion.Usage.OutputTokenCount,
                    TotalTokens = completion.Usage.TotalTokenCount
                }
        };
    }

    private static string ExtractText(ChatMessageContent content)
    {
        if (content is null || content.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var part in content)
        {
            if (!string.IsNullOrEmpty(part.Text))
            {
                builder.Append(part.Text);
            }
        }

        return builder.ToString();
    }

    private static AgentFinishReason MapFinishReason(ChatFinishReason finishReason) =>
        finishReason switch
        {
            ChatFinishReason.Stop => AgentFinishReason.Stop,
            ChatFinishReason.Length => AgentFinishReason.Length,
            ChatFinishReason.ToolCalls => AgentFinishReason.ToolCalls,
            ChatFinishReason.ContentFilter => AgentFinishReason.ContentFilter,
            _ => AgentFinishReason.Unknown
        };
}
