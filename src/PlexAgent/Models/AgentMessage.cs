namespace PlexAgent.Models;

/// <summary>A single message in an agent conversation.</summary>
public sealed class AgentMessage
{
    public AgentRole Role { get; init; }

    public string Content { get; init; } = string.Empty;

    /// <summary>Optional name (e.g. tool name for <see cref="AgentRole.Tool"/>).</summary>
    public string? Name { get; init; }

    public string? ToolCallId { get; init; }

    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }

    public static AgentMessage System(string content) => new() { Role = AgentRole.System, Content = content };

    public static AgentMessage User(string content) => new() { Role = AgentRole.User, Content = content };

    public static AgentMessage Assistant(string content) => new() { Role = AgentRole.Assistant, Content = content };

    public static AgentMessage Assistant(string content, IReadOnlyList<ToolCall> toolCalls) => new()
    {
        Role = AgentRole.Assistant,
        Content = content,
        ToolCalls = toolCalls
    };

    public static AgentMessage Tool(string toolCallId, string name, string content) => new()
    {
        Role = AgentRole.Tool,
        ToolCallId = toolCallId,
        Name = name,
        Content = content
    };
}
