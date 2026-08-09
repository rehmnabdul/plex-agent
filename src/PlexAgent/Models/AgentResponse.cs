namespace PlexAgent.Models;

/// <summary>Unified agent completion response.</summary>
public class AgentResponse
{
    public string Content { get; init; } = string.Empty;

    /// <summary>New messages produced by this turn (assistant / tool messages).</summary>
    public IReadOnlyList<AgentMessage> Messages { get; init; } = Array.Empty<AgentMessage>();

    public AgentUsage? Usage { get; init; }

    public string ProviderId { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public AgentFinishReason FinishReason { get; init; } = AgentFinishReason.Stop;

    public IReadOnlyList<ToolExecutionRecord>? ToolsExecuted { get; init; }
}

/// <summary>Typed structured-output response.</summary>
/// <typeparam name="T">Deserialized JSON payload type.</typeparam>
public sealed class AgentResponse<T> : AgentResponse
{
    public required T Data { get; init; }
}
