namespace PlexAgent.Configuration;

/// <summary>Root configuration bound from the <c>PlexAgent</c> section.</summary>
public sealed class PlexAgentOptions
{
    public const string SectionName = "PlexAgent";

    public string? DefaultAgent { get; set; }

    public ToolLoopOptions ToolLoop { get; set; } = new();

    public bool EnableSensitiveLogging { get; set; }

    public Dictionary<string, AgentDefinitionOptions> Agents { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Tool-loop safety limits.</summary>
public sealed class ToolLoopOptions
{
    public int MaxIterations { get; set; } = 10;
}

/// <summary>Definition of a named agent.</summary>
public sealed class AgentDefinitionOptions
{
    public string Provider { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string? SystemPrompt { get; set; }

    public AgentParameterOptions Parameters { get; set; } = new();

    /// <summary>Default response format name: Text, JsonObject, or JsonSchema.</summary>
    public string ResponseFormat { get; set; } = "Text";

    public List<string> ToolNames { get; set; } = [];
}

/// <summary>Default generation parameters for an agent.</summary>
public sealed class AgentParameterOptions
{
    public float? Temperature { get; set; }

    public int? MaxTokens { get; set; }

    public float? TopP { get; set; }
}

/// <summary>Shared provider connection options.</summary>
public abstract class ProviderOptionsBase
{
    public string ApiKey { get; set; } = string.Empty;

    public string? BaseUrl { get; set; }

    public string? DefaultModel { get; set; }

    public int TimeoutSeconds { get; set; } = 120;
}
