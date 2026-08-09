using System.Text.Json;
using PlexAgent.Models;

namespace PlexAgent.Abstractions;

/// <summary>Unified completion request sent to provider adapters.</summary>
public sealed class ProviderCompletionRequest
{
    public required string Model { get; init; }

    public required IReadOnlyList<AgentMessage> Messages { get; init; }

    public float? Temperature { get; init; }

    public int? MaxTokens { get; init; }

    public float? TopP { get; init; }

    public IReadOnlyList<string>? StopSequences { get; init; }

    public ResponseFormatKind ResponseFormat { get; init; } = ResponseFormatKind.Text;

    public JsonSchemaResponseFormat? JsonSchema { get; init; }

    public IReadOnlyList<ProviderToolDefinition>? Tools { get; init; }
}

/// <summary>Tool definition forwarded to a provider in its native mapping layer.</summary>
public sealed class ProviderToolDefinition
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required JsonDocument ParameterSchema { get; init; }
}

/// <summary>Unified completion result from provider adapters.</summary>
public sealed class ProviderCompletionResult
{
    public required string Content { get; init; }

    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }

    public AgentUsage? Usage { get; init; }

    public AgentFinishReason FinishReason { get; init; } = AgentFinishReason.Stop;

    public required string Model { get; init; }
}
