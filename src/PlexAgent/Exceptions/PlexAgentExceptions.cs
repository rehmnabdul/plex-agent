namespace PlexAgent.Exceptions;

/// <summary>Base exception for Plex Agent failures.</summary>
public class PlexAgentException : Exception
{
    /// <summary>Creates a Plex Agent exception.</summary>
    public PlexAgentException(string message)
        : base(message)
    {
    }

    /// <summary>Creates a Plex Agent exception with an inner exception.</summary>
    public PlexAgentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Optional agent name associated with the failure.</summary>
    public string? AgentName { get; init; }

    /// <summary>Optional provider id associated with the failure.</summary>
    public string? ProviderId { get; init; }

    /// <summary>Optional model id associated with the failure.</summary>
    public string? Model { get; init; }
}

/// <summary>Thrown when a requested agent name is not configured.</summary>
public sealed class AgentNotFoundException : PlexAgentException
{
    /// <summary>Creates an agent-not-found exception.</summary>
    public AgentNotFoundException(string agentName)
        : base($"Agent '{agentName}' was not registered.")
    {
        AgentName = agentName;
    }
}

/// <summary>Thrown when an LLM provider id is not registered in DI.</summary>
public sealed class ProviderNotRegisteredException : PlexAgentException
{
    /// <summary>Creates a provider-not-registered exception.</summary>
    public ProviderNotRegisteredException(string providerId)
        : base($"LLM provider '{providerId}' is not registered. Call the corresponding Add* extension.")
    {
        ProviderId = providerId;
    }
}

/// <summary>Thrown when provider configuration is invalid or incomplete.</summary>
public sealed class ProviderConfigurationException : PlexAgentException
{
    /// <summary>Creates a provider configuration exception.</summary>
    public ProviderConfigurationException(string providerId, string message)
        : base(message)
    {
        ProviderId = providerId;
    }
}

/// <summary>Thrown when a provider HTTP/API request fails.</summary>
public sealed class ProviderRequestException : PlexAgentException
{
    /// <summary>Creates a provider request exception.</summary>
    public ProviderRequestException(string providerId, string message)
        : base(message)
    {
        ProviderId = providerId;
    }
}

/// <summary>Thrown when an agent requires tools but the selected provider/model cannot call tools.</summary>
public sealed class ToolCallingNotSupportedException : PlexAgentException
{
    /// <summary>Creates a tool-calling-not-supported exception.</summary>
    public ToolCallingNotSupportedException(string agentName, string providerId, string model)
        : base($"Agent '{agentName}' has tools, but provider '{providerId}' model '{model}' does not support tool calling.")
    {
        AgentName = agentName;
        ProviderId = providerId;
        Model = model;
    }
}

/// <summary>Thrown when structured JSON output is requested but unsupported by the provider/model.</summary>
public sealed class StructuredOutputNotSupportedException : PlexAgentException
{
    /// <summary>Creates a structured-output-not-supported exception.</summary>
    public StructuredOutputNotSupportedException(string providerId, string model)
        : base($"Provider '{providerId}' model '{model}' does not support the requested structured JSON response format.")
    {
        ProviderId = providerId;
        Model = model;
    }
}

/// <summary>Thrown when structured JSON parsing, validation, or deserialization fails.</summary>
public sealed class StructuredOutputException : PlexAgentException
{
    /// <summary>Creates a structured-output exception.</summary>
    public StructuredOutputException(string message, string? rawContent = null)
        : base(message)
    {
        RawContent = rawContent;
    }

    /// <summary>Creates a structured-output exception with an inner exception.</summary>
    public StructuredOutputException(string message, string? rawContent, Exception innerException)
        : base(message, innerException)
    {
        RawContent = rawContent;
    }

    /// <summary>Raw model content that failed validation/deserialization, when available.</summary>
    public string? RawContent { get; }
}

/// <summary>Thrown when the tool loop exceeds the configured max iterations.</summary>
public sealed class ToolLoopMaxIterationsExceededException : PlexAgentException
{
    /// <summary>Creates a max-iterations-exceeded exception.</summary>
    public ToolLoopMaxIterationsExceededException(string agentName, int maxIterations)
        : base($"Agent '{agentName}' exceeded the tool-loop max iterations ({maxIterations}).")
    {
        AgentName = agentName;
        MaxIterations = maxIterations;
    }

    /// <summary>Configured maximum iterations.</summary>
    public int MaxIterations { get; }
}

/// <summary>Thrown when an agent references a tool that was not registered.</summary>
public sealed class ToolNotFoundException : PlexAgentException
{
    /// <summary>Creates a tool-not-found exception.</summary>
    public ToolNotFoundException(string toolName)
        : base($"Tool '{toolName}' was not registered.")
    {
        ToolName = toolName;
    }

    /// <summary>Missing tool name.</summary>
    public string ToolName { get; }
}

/// <summary>Thrown when tool argument validation or tool handler execution fails.</summary>
public sealed class ToolExecutionException : PlexAgentException
{
    /// <summary>Creates a tool-execution exception.</summary>
    public ToolExecutionException(string toolName, string message)
        : base(message)
    {
        ToolName = toolName;
    }

    /// <summary>Creates a tool-execution exception with an inner exception.</summary>
    public ToolExecutionException(string toolName, string message, Exception innerException)
        : base(message, innerException)
    {
        ToolName = toolName;
    }

    /// <summary>Tool that failed.</summary>
    public string ToolName { get; }
}
