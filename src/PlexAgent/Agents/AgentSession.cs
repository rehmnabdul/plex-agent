using PlexAgent.Abstractions;
using PlexAgent.Configuration;
using PlexAgent.Models;

namespace PlexAgent.Agents;

internal sealed class AgentSession : IAgentSession
{
    private readonly Agent _agent;
    private readonly AgentDefinitionOptions _definition;
    private readonly List<AgentMessage> _history = [];
    private bool _systemPromptInjected;

    public AgentSession(Agent agent, AgentDefinitionOptions definition)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(definition);
        _agent = agent;
        _definition = definition;
        AgentName = agent.Name;
        EnsureSystemPrompt();
    }

    public string AgentName { get; }

    public IReadOnlyList<AgentMessage> History => _history;

    public async Task<AgentResponse> AskAsync(
        string prompt,
        Action<AgentRequestOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        EnsureSystemPrompt();
        _history.Add(AgentMessage.User(prompt));

        var response = await _agent.AskAsync(_history, configure, cancellationToken).ConfigureAwait(false);
        AppendResponse(response);
        return response;
    }

    public async Task<AgentResponse<T>> AskAsync<T>(
        string prompt,
        Action<AgentRequestOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        EnsureSystemPrompt();
        _history.Add(AgentMessage.User(prompt));

        var response = await _agent.AskAsync<T>(_history, configure, cancellationToken).ConfigureAwait(false);
        AppendResponse(response);
        return response;
    }

    public void Clear()
    {
        _history.Clear();
        _systemPromptInjected = false;
    }

    private void EnsureSystemPrompt()
    {
        if (_systemPromptInjected || string.IsNullOrWhiteSpace(_definition.SystemPrompt))
        {
            return;
        }

        _history.Insert(0, AgentMessage.System(_definition.SystemPrompt));
        _systemPromptInjected = true;
    }

    private void AppendResponse(AgentResponse response)
    {
        foreach (var message in response.Messages)
        {
            _history.Add(message);
        }
    }
}
