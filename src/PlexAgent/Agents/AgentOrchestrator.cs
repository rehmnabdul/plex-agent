using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlexAgent.Abstractions;
using PlexAgent.Configuration;
using PlexAgent.Exceptions;
using PlexAgent.Internal;
using PlexAgent.Models;
using PlexAgent.StructuredOutput;
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

        StructuredOutputPlanner.ApplyExplicitFormat(requestOptions, capabilities, providerId, model);
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

    public async IAsyncEnumerable<AgentStreamEvent> StreamAsync(
        string agentName,
        AgentDefinitionOptions definition,
        IReadOnlyList<AgentMessage> messages,
        AgentRequestOptions requestOptions,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
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
        if (!capabilities.SupportsStreaming)
        {
            throw new StreamingNotSupportedException(agentName, providerId, model);
        }

        StructuredOutputPlanner.ApplyExplicitFormat(requestOptions, capabilities, providerId, model);
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
                "Streaming agent {AgentName} via {ProviderId}/{Model} (iteration {Iteration})",
                agentName,
                providerId,
                model,
                iteration);

            ProviderCompletionResult? lastResult = null;
            await foreach (var update in provider.StreamAsync(request, cancellationToken).ConfigureAwait(false))
            {
                if (!string.IsNullOrEmpty(update.TextDelta))
                {
                    yield return new AgentStreamEvent
                    {
                        Kind = AgentStreamEventKind.ContentDelta,
                        TextDelta = update.TextDelta
                    };
                }

                if (update.Completed is not null)
                {
                    lastResult = update.Completed;
                }
            }

            if (lastResult is null)
            {
                throw new ProviderRequestException(
                    providerId,
                    $"Provider '{providerId}' ended the stream without a completion payload.");
            }

            totalUsage = AggregateUsage(totalUsage, lastResult.Usage);

            if (lastResult.ToolCalls is not { Count: > 0 })
            {
                var assistantMessage = AgentMessage.Assistant(lastResult.Content);
                conversation.Add(assistantMessage);
                producedMessages.Add(assistantMessage);

                yield return new AgentStreamEvent
                {
                    Kind = AgentStreamEventKind.Completed,
                    Response = new AgentResponse
                    {
                        Content = lastResult.Content,
                        Messages = producedMessages,
                        Usage = totalUsage,
                        ProviderId = providerId,
                        Model = lastResult.Model,
                        FinishReason = lastResult.FinishReason,
                        ToolsExecuted = executedTools.Count == 0 ? null : executedTools
                    }
                };
                yield break;
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
                yield return new AgentStreamEvent
                {
                    Kind = AgentStreamEventKind.ToolCall,
                    ToolCall = toolCall
                };

                var toolResult = await _toolExecutor.ExecuteAsync(toolCall, cancellationToken).ConfigureAwait(false);
                var toolMessage = AgentMessage.Tool(toolResult.ToolCallId, toolResult.Name, toolResult.Content);
                conversation.Add(toolMessage);
                producedMessages.Add(toolMessage);
                var record = new ToolExecutionRecord
                {
                    Name = toolCall.Name,
                    ArgumentsJson = toolCall.ArgumentsJson,
                    ResultJson = toolResult.Content,
                    IsError = toolResult.IsError
                };
                executedTools.Add(record);
                yield return new AgentStreamEvent
                {
                    Kind = AgentStreamEventKind.ToolResult,
                    ToolExecution = record
                };
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
        var providerId = string.IsNullOrWhiteSpace(requestOptions.ProviderId)
            ? definition.Provider
            : requestOptions.ProviderId;
        var model = string.IsNullOrWhiteSpace(requestOptions.Model)
            ? definition.Model
            : requestOptions.Model;

        if (string.IsNullOrWhiteSpace(providerId) || string.IsNullOrWhiteSpace(model))
        {
            // CompleteAsync performs the definitive configuration validation.
            providerId ??= string.Empty;
            model ??= string.Empty;
        }
        else
        {
            var provider = _providers.GetRequired(providerId);
            var capabilities = await provider.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
            StructuredOutputPlanner.ApplyForType<T>(requestOptions, capabilities, providerId, model);
        }

        var response = await CompleteAsync(agentName, definition, messages, requestOptions, cancellationToken)
            .ConfigureAwait(false);

        if (requestOptions.JsonSchema is not null)
        {
            JsonSchemaValidator.ValidateStructured(
                requestOptions.JsonSchema.Schema,
                response.Content,
                typeof(T).Name);
        }

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
        // Per-call format wins. JsonSchema may still be present for post-validation
        // when the provider only supports JSON object mode.
        if (requestOptions.ResponseFormat != ResponseFormatKind.Text)
        {
            return requestOptions.ResponseFormat;
        }

        if (requestOptions.JsonSchema is not null)
        {
            return ResponseFormatKind.JsonSchema;
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
            case ResponseFormatKind.JsonSchema when !capabilities.SupportsJsonSchema:
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
