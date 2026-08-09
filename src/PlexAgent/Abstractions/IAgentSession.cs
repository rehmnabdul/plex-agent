using PlexAgent.Models;

namespace PlexAgent.Abstractions;

/// <summary>Multi-turn conversation session for a named agent.</summary>
public interface IAgentSession
{
    /// <summary>Parent agent name.</summary>
    string AgentName { get; }

    /// <summary>Current conversation history, including system prompt when present.</summary>
    IReadOnlyList<AgentMessage> History { get; }

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

    /// <summary>Clears conversation history (system prompt is re-injected on the next turn).</summary>
    void Clear();
}
