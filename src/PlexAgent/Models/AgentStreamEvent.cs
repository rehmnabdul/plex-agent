namespace PlexAgent.Models;

/// <summary>Kind of event emitted during <c>StreamAsync</c>.</summary>
public enum AgentStreamEventKind
{
    /// <summary>Incremental assistant text.</summary>
    ContentDelta = 0,

    /// <summary>A tool call requested by the model.</summary>
    ToolCall = 1,

    /// <summary>A tool execution result.</summary>
    ToolResult = 2,

    /// <summary>Final aggregated response for the turn.</summary>
    Completed = 3
}

/// <summary>A single streaming event from an agent turn.</summary>
public sealed class AgentStreamEvent
{
    /// <summary>Event kind.</summary>
    public required AgentStreamEventKind Kind { get; init; }

    /// <summary>Text delta when <see cref="Kind"/> is <see cref="AgentStreamEventKind.ContentDelta"/>.</summary>
    public string? TextDelta { get; init; }

    /// <summary>Tool call when <see cref="Kind"/> is <see cref="AgentStreamEventKind.ToolCall"/>.</summary>
    public ToolCall? ToolCall { get; init; }

    /// <summary>Tool execution record when <see cref="Kind"/> is <see cref="AgentStreamEventKind.ToolResult"/>.</summary>
    public ToolExecutionRecord? ToolExecution { get; init; }

    /// <summary>Final response when <see cref="Kind"/> is <see cref="AgentStreamEventKind.Completed"/>.</summary>
    public AgentResponse? Response { get; init; }
}
