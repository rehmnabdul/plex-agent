using System.Text.Json;

namespace PlexAgent.Models;

/// <summary>A model-requested tool invocation.</summary>
public sealed class ToolCall
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string ArgumentsJson { get; init; } = "{}";
}

/// <summary>Result returned after executing a tool.</summary>
public sealed class ToolResult
{
    public string ToolCallId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public bool IsError { get; init; }
}

/// <summary>Record of a tool execution performed during orchestration.</summary>
public sealed class ToolExecutionRecord
{
    public string Name { get; init; } = string.Empty;

    public string ArgumentsJson { get; init; } = "{}";

    public string ResultJson { get; init; } = string.Empty;

    public bool IsError { get; init; }
}

/// <summary>Token usage reported by a provider.</summary>
public sealed class AgentUsage
{
    public int? InputTokens { get; init; }

    public int? OutputTokens { get; init; }

    public int? TotalTokens { get; init; }
}

/// <summary>JSON schema response format for structured outputs.</summary>
public sealed class JsonSchemaResponseFormat
{
    public string Name { get; init; } = "response";

    public string? Description { get; init; }

    public required JsonDocument Schema { get; init; }

    public bool Strict { get; init; } = true;
}
