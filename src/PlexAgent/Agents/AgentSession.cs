using Microsoft.Extensions.Options;
using PlexAgent.Abstractions;
using PlexAgent.Configuration;
using PlexAgent.Models;

namespace PlexAgent.Agents;

internal sealed class AgentSession : IAgentSession
{
    private readonly Agent _agent;
    private readonly AgentDefinitionOptions _definition;
    private readonly SessionOptions _sessionOptions;
    private readonly List<AgentMessage> _history = [];
    private bool _systemPromptInjected;

    public AgentSession(
        Agent agent,
        AgentDefinitionOptions definition,
        IOptions<PlexAgentOptions> options)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(options);

        _agent = agent;
        _definition = definition;
        _sessionOptions = options.Value.Sessions;
        AgentName = agent.Name;
        EnsureSystemPrompt();
    }

    public string AgentName { get; }

    public IReadOnlyList<AgentMessage> History => _history;

    public int TurnCount { get; private set; }

    public string? LastProviderId { get; private set; }

    public string? LastModel { get; private set; }

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

    public async IAsyncEnumerable<AgentStreamEvent> StreamAsync(
        string prompt,
        Action<AgentRequestOptions>? configure = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        EnsureSystemPrompt();
        _history.Add(AgentMessage.User(prompt));

        AgentResponse? completed = null;
        await foreach (var streamEvent in _agent.StreamAsync(_history, configure, cancellationToken).ConfigureAwait(false))
        {
            if (streamEvent.Kind == AgentStreamEventKind.Completed && streamEvent.Response is not null)
            {
                completed = streamEvent.Response;
            }

            yield return streamEvent;
        }

        if (completed is not null)
        {
            AppendResponse(completed);
        }
    }

    public void Clear()
    {
        _history.Clear();
        _systemPromptInjected = false;
        TurnCount = 0;
        LastProviderId = null;
        LastModel = null;
    }

    public void Reset()
    {
        Clear();
        EnsureSystemPrompt();
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

        TurnCount++;
        LastProviderId = response.ProviderId;
        LastModel = response.Model;
        TrimHistoryIfNeeded();
    }

    private void TrimHistoryIfNeeded()
    {
        var max = _sessionOptions.MaxHistoryMessages;
        if (max <= 0 || _history.Count <= max)
        {
            return;
        }

        // Always keep a leading system prompt when present; drop oldest non-system messages.
        var hasSystem = _history.Count > 0 && _history[0].Role == AgentRole.System;
        var keepFrom = _history.Count - max;
        if (hasSystem)
        {
            keepFrom = Math.Max(1, keepFrom);
            var trimmed = new List<AgentMessage>(max) { _history[0] };
            trimmed.AddRange(_history.Skip(keepFrom));
            // If still over (system + rest), drop more from the front of the non-system slice.
            while (trimmed.Count > max && trimmed.Count > 1)
            {
                trimmed.RemoveAt(1);
            }

            _history.Clear();
            _history.AddRange(trimmed);
            return;
        }

        _history.RemoveRange(0, _history.Count - max);
    }
}
