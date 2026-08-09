using System.Text.Json;
using Microsoft.Extensions.Logging;
using PlexAgent.Abstractions;
using PlexAgent.Configuration;
using PlexAgent.Exceptions;
using PlexAgent.Internal;
using PlexAgent.Models;

namespace PlexAgent.Agents;

internal sealed class AgentOrchestrator
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly LlmProviderRegistry _providers;
    private readonly ILogger<AgentOrchestrator> _logger;

    public AgentOrchestrator(
        LlmProviderRegistry providers,
        ILogger<AgentOrchestrator> logger)
    {
        _providers = providers;
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

        // Phase 1: single-shot completion only; tool loop lands in Phase 4.
        if (definition.ToolNames.Count > 0)
        {
            _logger.LogWarning(
                "Agent {AgentName} declares {ToolCount} tools, but tool calling is not enabled yet; continuing with text completion.",
                agentName,
                definition.ToolNames.Count);
        }

        var preparedMessages = PrepareMessages(definition, messages);
        var request = new ProviderCompletionRequest
        {
            Model = model,
            Messages = preparedMessages,
            Temperature = requestOptions.Temperature ?? definition.Parameters.Temperature,
            MaxTokens = requestOptions.MaxTokens ?? definition.Parameters.MaxTokens,
            TopP = requestOptions.TopP ?? definition.Parameters.TopP,
            StopSequences = requestOptions.StopSequences,
            ResponseFormat = responseFormat,
            JsonSchema = requestOptions.JsonSchema,
            Tools = null
        };

        _logger.LogDebug(
            "Completing agent {AgentName} via {ProviderId}/{Model}",
            agentName,
            providerId,
            model);

        var result = await provider.CompleteAsync(request, cancellationToken).ConfigureAwait(false);

        var assistantMessage = AgentMessage.Assistant(result.Content);
        return new AgentResponse
        {
            Content = result.Content,
            Messages = [assistantMessage],
            Usage = result.Usage,
            ProviderId = providerId,
            Model = result.Model,
            FinishReason = result.FinishReason,
            ToolsExecuted = null
        };
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
