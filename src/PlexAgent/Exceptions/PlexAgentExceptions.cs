namespace PlexAgent.Exceptions;

/// <summary>Base exception for Plex Agent failures.</summary>
public class PlexAgentException : Exception
{
    public PlexAgentException(string message)
        : base(message)
    {
    }

    public PlexAgentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public string? AgentName { get; init; }

    public string? ProviderId { get; init; }

    public string? Model { get; init; }
}

public sealed class AgentNotFoundException : PlexAgentException
{
    public AgentNotFoundException(string agentName)
        : base($"Agent '{agentName}' was not registered.")
    {
        AgentName = agentName;
    }
}

public sealed class ProviderNotRegisteredException : PlexAgentException
{
    public ProviderNotRegisteredException(string providerId)
        : base($"LLM provider '{providerId}' is not registered. Call the corresponding Add* extension.")
    {
        ProviderId = providerId;
    }
}

public sealed class ProviderConfigurationException : PlexAgentException
{
    public ProviderConfigurationException(string providerId, string message)
        : base(message)
    {
        ProviderId = providerId;
    }
}

public sealed class ProviderRequestException : PlexAgentException
{
    public ProviderRequestException(string providerId, string message)
        : base(message)
    {
        ProviderId = providerId;
    }
}

public sealed class ToolCallingNotSupportedException : PlexAgentException
{
    public ToolCallingNotSupportedException(string agentName, string providerId, string model)
        : base($"Agent '{agentName}' has tools, but provider '{providerId}' model '{model}' does not support tool calling.")
    {
        AgentName = agentName;
        ProviderId = providerId;
        Model = model;
    }
}

public sealed class StructuredOutputNotSupportedException : PlexAgentException
{
    public StructuredOutputNotSupportedException(string providerId, string model)
        : base($"Provider '{providerId}' model '{model}' does not support the requested structured JSON response format.")
    {
        ProviderId = providerId;
        Model = model;
    }
}

public sealed class StructuredOutputException : PlexAgentException
{
    public StructuredOutputException(string message, string? rawContent = null)
        : base(message)
    {
        RawContent = rawContent;
    }

    public StructuredOutputException(string message, string? rawContent, Exception innerException)
        : base(message, innerException)
    {
        RawContent = rawContent;
    }

    public string? RawContent { get; }
}

public sealed class ToolLoopMaxIterationsExceededException : PlexAgentException
{
    public ToolLoopMaxIterationsExceededException(string agentName, int maxIterations)
        : base($"Agent '{agentName}' exceeded the tool-loop max iterations ({maxIterations}).")
    {
        AgentName = agentName;
        MaxIterations = maxIterations;
    }

    public int MaxIterations { get; }
}
