using Microsoft.Extensions.Options;
using PlexAgent.Abstractions;
using PlexAgent.Configuration;
using PlexAgent.Models;

namespace PlexAgent.Agents;

internal sealed class Agent : IAgent
{
    private readonly AgentDefinitionOptions _definition;
    private readonly AgentOrchestrator _orchestrator;
    private readonly IOptions<PlexAgentOptions> _options;

    public Agent(
        string name,
        AgentDefinitionOptions definition,
        AgentOrchestrator orchestrator,
        IOptions<PlexAgentOptions> options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(options);

        Name = name;
        _definition = definition;
        _orchestrator = orchestrator;
        _options = options;
    }

    public string Name { get; }

    public string DefaultProviderId => _definition.Provider;

    public string DefaultModel => _definition.Model;

    public Task<AgentResponse> AskAsync(
        string prompt,
        Action<AgentRequestOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        return AskAsync([AgentMessage.User(prompt)], configure, cancellationToken);
    }

    public Task<AgentResponse<T>> AskAsync<T>(
        string prompt,
        Action<AgentRequestOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        return AskAsync<T>([AgentMessage.User(prompt)], configure, cancellationToken);
    }

    public Task<AgentResponse> AskAsync(
        string prompt,
        JsonSchemaResponseFormat schema,
        Action<AgentRequestOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(schema);

        return AskAsync(
            [AgentMessage.User(prompt)],
            opts =>
            {
                configure?.Invoke(opts);
                opts.WithJsonSchema(schema);
            },
            cancellationToken);
    }

    public Task<AgentResponse<T>> AskAsync<T>(
        string prompt,
        JsonSchemaResponseFormat schema,
        Action<AgentRequestOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(schema);

        return AskAsync<T>(
            [AgentMessage.User(prompt)],
            opts =>
            {
                configure?.Invoke(opts);
                opts.WithJsonSchema(schema);
            },
            cancellationToken);
    }

    public Task<AgentResponse> AskAsync(
        IReadOnlyList<AgentMessage> messages,
        Action<AgentRequestOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var options = CreateOptions(configure);
        return _orchestrator.CompleteAsync(Name, _definition, messages, options, cancellationToken);
    }

    public Task<AgentResponse<T>> AskAsync<T>(
        IReadOnlyList<AgentMessage> messages,
        Action<AgentRequestOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var options = CreateOptions(configure);
        return _orchestrator.CompleteStructuredAsync<T>(Name, _definition, messages, options, cancellationToken);
    }

    public IAgentSession CreateSession() => new AgentSession(this, _definition, _options);

    private static AgentRequestOptions CreateOptions(Action<AgentRequestOptions>? configure)
    {
        var options = new AgentRequestOptions();
        configure?.Invoke(options);
        return options;
    }
}
