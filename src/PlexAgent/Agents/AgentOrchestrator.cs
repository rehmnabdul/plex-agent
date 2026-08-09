using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlexAgent.Abstractions;
using PlexAgent.Configuration;
using PlexAgent.Exceptions;
using PlexAgent.Internal;
using PlexAgent.Models;
using PlexAgent.Tools;

namespace PlexAgent.Agents;

internal sealed class AgentOrchestrator
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly LlmProviderRegistry _providers;
    private readonly ToolRegistry _tools;
    private readonly IToolExecutor _toolExecutor;
    private readonly IOptions<PlexAgentOptions> _options;
    private readonly ILogger<AgentOrchestrator> _logger;

    public AgentOrchestrator(
        LlmProviderRegistry providers,
        ToolRegistry tools,
        IToolExecutor toolExecutor,
        IOptions<PlexAgentOptions> options,
        ILogger<AgentOrchestrator> logger)
    {
        _providers = providers;
        _tools = tools;
        _toolExecutor = toolExecutor;
        _options = options;
        _logger = logger;
    }

    public async Task<AgentResponse> CompleteAsync(
        string agentName,
        AgentDefinitionOptions definition,
        IReadOnlyList<AgentMessage> messages,
        AgentRequestOptions requestOptions,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(requestOptions);

        var providerId = string.IsNullOrWhiteSpace(requestOptions.ProviderId)
            ? definition.Provider
            : requestOptions.ProviderId;
        var model = string.IsNullOrWhiteSpace(requestOptions.Model)
            ? definition.Model
            : requestOptions.Model;

        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new ProviderConfigurationException(
                providerId ?? string.Empty,
                $"Agent '{agentName}' does not specify a provider.");
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ProviderConfigurationException(
                providerId,
                $"Agent '{agentName}' does not specify a model.");
        }

        var provider = _providers.GetRequired(providerId);
        var capabilities = await provider.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);

        var responseFormat = ResolveResponseFormat(definition, requestOptions);
        EnsureStructuredOutputSupported(providerId, model, responseFormat, capabilities);

        var resolvedTools = _tools.ResolveMany(definition.ToolNames);
        if (resolvedTools.Count > 0 && !capabilities.SupportsToolCalling)
        {
            throw new ToolCallingNotSupportedException(agentName, providerId, model);
        }

        var providerTools = resolvedTools.Count == 0
            ? null
            : resolvedTools
                .Select(static t => new ProviderToolDefinition
                {
                    Name = t.Name,
                    Description = t.Description,
                    ParameterSchema = t.ParameterSchema
                })
                .ToArray();

        var conversation = PrepareMessages(definition, messages).ToList();
        var producedMessages = new List<AgentMessage>();
        var executedTools = new List<ToolExecutionRecord>();
        var maxIterations = Math.Max(1, _options.Value.ToolLoop.MaxIterations);
        AgentUsage? totalUsage = null;
        ProviderCompletionResult? lastResult = null;

        for (var iteration = 1; iteration <= maxIterations; iteration++)
        {
            var request = new ProviderCompletionRequest
            {
                Model = model,
                Messages = conversation,
                Temperature = requestOptions.Temperature ?? definition.Parameters.Temperature,
                MaxTokens = requestOptions.MaxTokens ?? definition.Parameters.MaxTokens,
                TopP = requestOptions.TopP ?? definition.Parameters.TopP,
                StopSequences = requestOptions.StopSequences,
                ResponseFormat = responseFormat,
                JsonSchema = requestOptions.JsonSchema,
                Tools = providerTools
            };

            _logger.LogDebug(
                "Completing agent {AgentName} via {ProviderId}/{Model} (iteration {Iteration})",
                agentName,
                providerId,
                model,
                iteration);

            lastResult = await provider.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
            totalUsage = AggregateUsage(totalUsage, lastResult.Usage);

            if (lastResult.ToolCalls is not { Count: > 0 })
            {
                var assistantMessage = AgentMessage.Assistant(lastResult.Content);
                conversation.Add(assistantMessage);
                producedMessages.Add(assistantMessage);

                return new AgentResponse
                {
                    Content = lastResult.Content,
                    Messages = producedMessages,
                    Usage = totalUsage,
                    ProviderId = providerId,
                    Model = lastResult.Model,
                    FinishReason = lastResult.FinishReason,
                    ToolsExecuted = executedTools.Count == 0 ? null : executedTools
                };
            }

            if (iteration >= maxIterations)
            {
                throw new ToolLoopMaxIterationsExceededException(agentName, maxIterations);
            }

            var assistantWithTools = AgentMessage.Assistant(lastResult.Content, lastResult.ToolCalls);
            conversation.Add(assistantWithTools);
            producedMessages.Add(assistantWithTools);

            foreach (var toolCall in lastResult.ToolCalls)
            {
                var toolResult = await _toolExecutor.ExecuteAsync(toolCall, cancellationToken).ConfigureAwait(false);
                var toolMessage = AgentMessage.Tool(toolResult.ToolCallId, toolResult.Name, toolResult.Content);
                conversation.Add(toolMessage);
                producedMessages.Add(toolMessage);
                executedTools.Add(new ToolExecutionRecord
                {
                    Name = toolCall.Name,
                    ArgumentsJson = toolCall.ArgumentsJson,
                    ResultJson = toolResult.Content,
                    IsError = toolResult.IsError
                });
            }
        }

        throw new ToolLoopMaxIterationsExceededException(agentName, maxIterations);
    }

    public async Task<AgentResponse<T>> CompleteStructuredAsync<T>(
        string agentName,
        AgentDefinitionOptions definition,
        IReadOnlyList<AgentMessage> messages,
        AgentRequestOptions requestOptions,
        CancellationToken cancellationToken)
    {
        if (requestOptions.ResponseFormat == ResponseFormatKind.Text)
        {
            requestOptions.ResponseFormat = ResponseFormatKind.JsonObject;
        }

        var response = await CompleteAsync(agentName, definition, messages, requestOptions, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var data = JsonSerializer.Deserialize<T>(response.Content, SerializerOptions);
            if (data is null)
            {
                throw new StructuredOutputException(
                    $"Structured response for agent '{agentName}' deserialized to null.",
                    response.Content);
            }

            return new AgentResponse<T>
            {
                Content = response.Content,
                Messages = response.Messages,
                Usage = response.Usage,
                ProviderId = response.ProviderId,
                Model = response.Model,
                FinishReason = response.FinishReason,
                ToolsExecuted = response.ToolsExecuted,
                Data = data
            };
        }
        catch (JsonException ex)
        {
            throw new StructuredOutputException(
                $"Failed to deserialize structured response for agent '{agentName}' into {typeof(T).Name}.",
                response.Content,
                ex);
        }
    }

    private static AgentUsage? AggregateUsage(AgentUsage? current, AgentUsage? next)
    {
        if (next is null)
        {
            return current;
        }

        if (current is null)
        {
            return next;
        }

        var input = (current.InputTokens ?? 0) + (next.InputTokens ?? 0);
        var output = (current.OutputTokens ?? 0) + (next.OutputTokens ?? 0);
        return new AgentUsage
        {
            InputTokens = input,
            OutputTokens = output,
            TotalTokens = input + output
        };
    }

    private static ResponseFormatKind ResolveResponseFormat(
        AgentDefinitionOptions definition,
        AgentRequestOptions requestOptions)
    {
        if (requestOptions.ResponseFormat != ResponseFormatKind.Text || requestOptions.JsonSchema is not null)
        {
            return requestOptions.JsonSchema is not null
                ? ResponseFormatKind.JsonSchema
                : requestOptions.ResponseFormat;
        }

        return definition.ResponseFormat.ToLowerInvariant() switch
        {
            "jsonobject" or "json_object" or "json" => ResponseFormatKind.JsonObject,
            "jsonschema" or "json_schema" => ResponseFormatKind.JsonSchema,
            _ => ResponseFormatKind.Text
        };
    }

    private static void EnsureStructuredOutputSupported(
        string providerId,
        string model,
        ResponseFormatKind format,
        ProviderCapabilities capabilities)
    {
        switch (format)
        {
            case ResponseFormatKind.JsonSchema when !capabilities.SupportsJsonSchema && !capabilities.SupportsJsonObject:
            case ResponseFormatKind.JsonObject when !capabilities.SupportsJsonObject && !capabilities.SupportsJsonSchema:
                throw new StructuredOutputNotSupportedException(providerId, model);
        }
    }

    private static IReadOnlyList<AgentMessage> PrepareMessages(
        AgentDefinitionOptions definition,
        IReadOnlyList<AgentMessage> messages)
    {
        if (string.IsNullOrWhiteSpace(definition.SystemPrompt))
        {
            return messages;
        }

        if (messages.Count > 0 && messages[0].Role == AgentRole.System)
        {
            return messages;
        }

        var prepared = new List<AgentMessage>(messages.Count + 1)
        {
            AgentMessage.System(definition.SystemPrompt)
        };
        prepared.AddRange(messages);
        return prepared;
    }
}
