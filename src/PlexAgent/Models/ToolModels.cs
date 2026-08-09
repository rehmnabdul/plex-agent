using System.Text.Json;

namespace PlexAgent.Models;

/// <summary>A model-requested tool invocation.</summary>
public sealed class ToolCall
{
    /// <summary>Provider-assigned tool call id.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Tool name to invoke.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>JSON object arguments for the tool.</summary>
    public string ArgumentsJson { get; init; } = "{}";
}

/// <summary>Result returned after executing a tool.</summary>
public sealed class ToolResult
{
    /// <summary>Matching tool call id.</summary>
    public string ToolCallId { get; init; } = string.Empty;

    /// <summary>Tool name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Serialized tool output content.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Whether the tool reported an error payload.</summary>
    public bool IsError { get; init; }
}

/// <summary>Record of a tool execution performed during orchestration.</summary>
public sealed class ToolExecutionRecord
{
    /// <summary>Tool name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Arguments JSON sent to the tool.</summary>
    public string ArgumentsJson { get; init; } = "{}";

    /// <summary>Result JSON returned by the tool.</summary>
    public string ResultJson { get; init; } = string.Empty;

    /// <summary>Whether the tool reported an error payload.</summary>
    public bool IsError { get; init; }
}

/// <summary>Token usage reported by a provider.</summary>
public sealed class AgentUsage
{
    /// <summary>Prompt/input tokens, when reported.</summary>
    public int? InputTokens { get; init; }

    /// <summary>Completion/output tokens, when reported.</summary>
    public int? OutputTokens { get; init; }

    /// <summary>Total tokens, when reported.</summary>
    public int? TotalTokens { get; init; }
}

/// <summary>JSON schema response format for structured outputs.</summary>
public sealed class JsonSchemaResponseFormat
{
    /// <summary>Schema name sent to providers that require one.</summary>
    public string Name { get; init; } = "response";

    /// <summary>Optional schema description.</summary>
    public string? Description { get; set; }

    /// <summary>JSON Schema document.</summary>
    public required JsonDocument Schema { get; init; }

    /// <summary>Whether strict schema adherence is requested when supported.</summary>
    public bool Strict { get; init; } = true;
}
