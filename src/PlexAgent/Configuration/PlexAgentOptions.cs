namespace PlexAgent.Configuration;

/// <summary>Root configuration bound from the <c>PlexAgent</c> section.</summary>
public sealed class PlexAgentOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "PlexAgent";

    /// <summary>Optional default agent name for host apps.</summary>
    public string? DefaultAgent { get; set; }

    /// <summary>Tool-loop safety limits.</summary>
    public ToolLoopOptions ToolLoop { get; set; } = new();

    /// <summary>Session history and retention defaults.</summary>
    public SessionOptions Sessions { get; set; } = new();

    /// <summary>When true, may log sensitive payloads (prompts/tool args). Default is false.</summary>
    public bool EnableSensitiveLogging { get; set; }

    /// <summary>Named agent definitions keyed by agent name.</summary>
    public Dictionary<string, AgentDefinitionOptions> Agents { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Tool-loop safety limits.</summary>
public sealed class ToolLoopOptions
{
    /// <summary>Maximum tool-loop iterations per request. Default is 10.</summary>
    public int MaxIterations { get; set; } = 10;
}

/// <summary>Session history retention settings.</summary>
public sealed class SessionOptions
{
    /// <summary>
    /// Maximum messages retained in a session history (including system).
    /// <c>0</c> means unlimited. When exceeded, oldest non-system messages are dropped.
    /// </summary>
    public int MaxHistoryMessages { get; set; }
}

/// <summary>Definition of a named agent.</summary>
public sealed class AgentDefinitionOptions
{
    /// <summary>Default provider id (for example <c>OpenAI</c>).</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Default model id.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Optional system prompt injected once per session / request preparation.</summary>
    public string? SystemPrompt { get; set; }

    /// <summary>Default generation parameters.</summary>
    public AgentParameterOptions Parameters { get; set; } = new();

    /// <summary>Default response format name: Text, JsonObject, or JsonSchema.</summary>
    public string ResponseFormat { get; set; } = "Text";

    /// <summary>Tool names this agent may invoke.</summary>
    public List<string> ToolNames { get; set; } = [];
}

/// <summary>Default generation parameters for an agent.</summary>
public sealed class AgentParameterOptions
{
    /// <summary>Sampling temperature.</summary>
    public float? Temperature { get; set; }

    /// <summary>Maximum output tokens.</summary>
    public int? MaxTokens { get; set; }

    /// <summary>Nucleus sampling probability.</summary>
    public float? TopP { get; set; }
}

/// <summary>Shared provider connection options.</summary>
public abstract class ProviderOptionsBase
{
    /// <summary>Provider API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Optional custom base URL.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Optional default model for the provider registration.</summary>
    public string? DefaultModel { get; set; }

    /// <summary>HTTP timeout in seconds. Default is 120.</summary>
    public int TimeoutSeconds { get; set; } = 120;
}
