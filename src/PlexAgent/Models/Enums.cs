namespace PlexAgent.Models;

/// <summary>Role of a message in an agent conversation.</summary>
public enum AgentRole
{
    System = 0,
    User = 1,
    Assistant = 2,
    Tool = 3
}

/// <summary>Why the model stopped generating.</summary>
public enum AgentFinishReason
{
    Stop = 0,
    Length = 1,
    ToolCalls = 2,
    ContentFilter = 3,
    Unknown = 4
}

/// <summary>Requested response format for a completion.</summary>
public enum ResponseFormatKind
{
    Text = 0,
    JsonObject = 1,
    JsonSchema = 2
}
