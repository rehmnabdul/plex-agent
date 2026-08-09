namespace PlexAgent.Models;

/// <summary>A single message in an agent conversation.</summary>
public sealed class AgentMessage
{
    /// <summary>Message role.</summary>
    public AgentRole Role { get; init; }

    /// <summary>Text content.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Optional name (e.g. tool name for <see cref="AgentRole.Tool"/>).</summary>
    public string? Name { get; init; }

    /// <summary>Tool call id for tool-result messages.</summary>
    public string? ToolCallId { get; init; }

    /// <summary>Tool calls requested by an assistant message.</summary>
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }

    /// <summary>Creates a system message.</summary>
    public static AgentMessage System(string content) => new() { Role = AgentRole.System, Content = content };

    /// <summary>Creates a user message.</summary>
    public static AgentMessage User(string content) => new() { Role = AgentRole.User, Content = content };

    /// <summary>Creates an assistant message.</summary>
    public static AgentMessage Assistant(string content) => new() { Role = AgentRole.Assistant, Content = content };

    /// <summary>Creates an assistant message that requests tool calls.</summary>
    public static AgentMessage Assistant(string content, IReadOnlyList<ToolCall> toolCalls) => new()
    {
        Role = AgentRole.Assistant,
        Content = content,
        ToolCalls = toolCalls
    };

    /// <summary>Creates a tool-result message.</summary>
    public static AgentMessage Tool(string toolCallId, string name, string content) => new()
    {
        Role = AgentRole.Tool,
        ToolCallId = toolCallId,
        Name = name,
        Content = content
    };
}
