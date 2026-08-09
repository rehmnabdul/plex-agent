namespace PlexAgent.Models;

/// <summary>Role of a message in an agent conversation.</summary>
public enum AgentRole
{
    /// <summary>System instructions.</summary>
    System = 0,

    /// <summary>End-user message.</summary>
    User = 1,

    /// <summary>Assistant/model message.</summary>
    Assistant = 2,

    /// <summary>Tool result message.</summary>
    Tool = 3
}

/// <summary>Why the model stopped generating.</summary>
public enum AgentFinishReason
{
    /// <summary>Natural stop.</summary>
    Stop = 0,

    /// <summary>Hit max length/tokens.</summary>
    Length = 1,

    /// <summary>Stopped to invoke tools.</summary>
    ToolCalls = 2,

    /// <summary>Stopped by a content filter.</summary>
    ContentFilter = 3,

    /// <summary>Unrecognized provider finish reason.</summary>
    Unknown = 4
}

/// <summary>Requested response format for a completion.</summary>
public enum ResponseFormatKind
{
    /// <summary>Plain text.</summary>
    Text = 0,

    /// <summary>JSON object without a fixed schema.</summary>
    JsonObject = 1,

    /// <summary>JSON constrained by a schema.</summary>
    JsonSchema = 2
}
