using System.Text.Json;
using PlexAgent.Models;

namespace PlexAgent.Abstractions;

/// <summary>Unified completion request sent to provider adapters.</summary>
public sealed class ProviderCompletionRequest
{
    /// <summary>Model id to use.</summary>
    public required string Model { get; init; }

    /// <summary>Conversation messages.</summary>
    public required IReadOnlyList<AgentMessage> Messages { get; init; }

    /// <summary>Optional temperature.</summary>
    public float? Temperature { get; init; }

    /// <summary>Optional max tokens.</summary>
    public int? MaxTokens { get; init; }

    /// <summary>Optional top-p.</summary>
    public float? TopP { get; init; }

    /// <summary>Optional stop sequences.</summary>
    public IReadOnlyList<string>? StopSequences { get; init; }

    /// <summary>Requested response format.</summary>
    public ResponseFormatKind ResponseFormat { get; init; } = ResponseFormatKind.Text;

    /// <summary>Optional JSON Schema for structured responses.</summary>
    public JsonSchemaResponseFormat? JsonSchema { get; init; }

    /// <summary>Optional tools available for this completion.</summary>
    public IReadOnlyList<ProviderToolDefinition>? Tools { get; init; }
}

/// <summary>Tool definition forwarded to a provider in its native mapping layer.</summary>
public sealed class ProviderToolDefinition
{
    /// <summary>Tool name.</summary>
    public required string Name { get; init; }

    /// <summary>Tool description for the model.</summary>
    public required string Description { get; init; }

    /// <summary>JSON Schema for tool parameters.</summary>
    public required JsonDocument ParameterSchema { get; init; }
}

/// <summary>Unified completion result from provider adapters.</summary>
public sealed class ProviderCompletionResult
{
    /// <summary>Assistant text content.</summary>
    public required string Content { get; init; }

    /// <summary>Tool calls requested by the model, when any.</summary>
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }

    /// <summary>Optional token usage.</summary>
    public AgentUsage? Usage { get; init; }

    /// <summary>Why generation stopped.</summary>
    public AgentFinishReason FinishReason { get; init; } = AgentFinishReason.Stop;

    /// <summary>Model that produced the result.</summary>
    public required string Model { get; init; }
}
