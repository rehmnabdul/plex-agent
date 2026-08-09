using PlexAgent.Models;

namespace PlexAgent.Abstractions;

/// <summary>Multi-turn conversation session for a named agent.</summary>
public interface IAgentSession
{
    /// <summary>Parent agent name.</summary>
    string AgentName { get; }

    /// <summary>Current conversation history, including system prompt when present.</summary>
    IReadOnlyList<AgentMessage> History { get; }

    /// <summary>Number of user turns completed in this session.</summary>
    int TurnCount { get; }

    /// <summary>Provider id used by the most recent turn, if any.</summary>
    string? LastProviderId { get; }

    /// <summary>Model id used by the most recent turn, if any.</summary>
    string? LastModel { get; }

    /// <summary>Appends a user prompt, runs the agent, and updates history.</summary>
    Task<AgentResponse> AskAsync(
        string prompt,
        Action<AgentRequestOptions>? configure = null,
        CancellationToken cancellationToken = default);

    /// <summary>Appends a user prompt and returns a structured response of type <typeparamref name="T"/>.</summary>
    Task<AgentResponse<T>> AskAsync<T>(
        string prompt,
        Action<AgentRequestOptions>? configure = null,
        CancellationToken cancellationToken = default);

    /// <summary>Streams a user prompt and updates history when the turn completes.</summary>
    IAsyncEnumerable<AgentStreamEvent> StreamAsync(
        string prompt,
        Action<AgentRequestOptions>? configure = null,
        CancellationToken cancellationToken = default);

    /// <summary>Clears conversation history. The system prompt is re-injected on the next turn.</summary>
    void Clear();

    /// <summary>Clears conversation history and immediately re-injects the system prompt when configured.</summary>
    void Reset();
}
