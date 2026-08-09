using Microsoft.Extensions.Options;
using PlexAgent.Abstractions;
using PlexAgent.Configuration;
using PlexAgent.Internal;

namespace PlexAgent.Agents;

internal sealed class AgentFactory : IAgentFactory
{
    private readonly AgentDefinitionRegistry _definitions;
    private readonly AgentOrchestrator _orchestrator;
    private readonly IOptions<PlexAgentOptions> _options;

    public AgentFactory(
        AgentDefinitionRegistry definitions,
        AgentOrchestrator orchestrator,
        IOptions<PlexAgentOptions> options)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(options);
        _definitions = definitions;
        _orchestrator = orchestrator;
        _options = options;
    }

    public IAgent GetAgent(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var (canonicalName, definition) = _definitions.GetRequired(name);
        return new Agent(canonicalName, definition, _orchestrator, _options);
    }

    public bool TryGetAgent(string name, out IAgent? agent)
    {
        agent = null;
        if (!_definitions.TryGet(name, out var canonicalName, out var definition) ||
            canonicalName is null ||
            definition is null)
        {
            return false;
        }

        agent = new Agent(canonicalName, definition, _orchestrator, _options);
        return true;
    }
}
