namespace PlexAgent.Models;

/// <summary>Unified agent completion response.</summary>
public class AgentResponse
{
    /// <summary>Final assistant text content for the turn.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>New messages produced by this turn (assistant / tool messages).</summary>
    public IReadOnlyList<AgentMessage> Messages { get; init; } = Array.Empty<AgentMessage>();

    /// <summary>Optional aggregated token usage.</summary>
    public AgentUsage? Usage { get; init; }

    /// <summary>Provider that produced the response.</summary>
    public string ProviderId { get; init; } = string.Empty;

    /// <summary>Model that produced the response.</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>Why generation stopped.</summary>
    public AgentFinishReason FinishReason { get; init; } = AgentFinishReason.Stop;

    /// <summary>Tools executed during the turn, when any.</summary>
    public IReadOnlyList<ToolExecutionRecord>? ToolsExecuted { get; init; }
}

/// <summary>Typed structured-output response.</summary>
/// <typeparam name="T">Deserialized JSON payload type.</typeparam>
public sealed class AgentResponse<T> : AgentResponse
{
    /// <summary>Deserialized structured payload.</summary>
    public required T Data { get; init; }
}
