using PlexAgent.Models;

namespace PlexAgent.Abstractions;

/// <summary>Named agent that can answer prompts and create multi-turn sessions.</summary>
public interface IAgent
{
    /// <summary>Unique agent name.</summary>
    string Name { get; }

    /// <summary>Default provider id from configuration or builder.</summary>
    string DefaultProviderId { get; }

    /// <summary>Default model id from configuration or builder.</summary>
    string DefaultModel { get; }

    /// <summary>Sends a single user prompt and returns a text response.</summary>
    Task<AgentResponse> AskAsync(
        string prompt,
        Action<AgentRequestOptions>? configure = null,
        CancellationToken cancellationToken = default);

    /// <summary>Sends a single user prompt and deserializes a structured JSON response into <typeparamref name="T"/>.</summary>
    Task<AgentResponse<T>> AskAsync<T>(
        string prompt,
        Action<AgentRequestOptions>? configure = null,
        CancellationToken cancellationToken = default);

    /// <summary>Sends a single user prompt with an explicit JSON schema response format.</summary>
    Task<AgentResponse> AskAsync(
        string prompt,
        JsonSchemaResponseFormat schema,
        Action<AgentRequestOptions>? configure = null,
        CancellationToken cancellationToken = default);

    /// <summary>Sends an explicit message list and returns a text response.</summary>
    Task<AgentResponse> AskAsync(
        IReadOnlyList<AgentMessage> messages,
        Action<AgentRequestOptions>? configure = null,
        CancellationToken cancellationToken = default);

    /// <summary>Sends an explicit message list and deserializes a structured JSON response into <typeparamref name="T"/>.</summary>
    Task<AgentResponse<T>> AskAsync<T>(
        IReadOnlyList<AgentMessage> messages,
        Action<AgentRequestOptions>? configure = null,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a multi-turn session that retains history.</summary>
    IAgentSession CreateSession();
}
