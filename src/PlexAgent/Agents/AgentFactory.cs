using PlexAgent.Abstractions;
using PlexAgent.Internal;

namespace PlexAgent.Agents;

internal sealed class AgentFactory : IAgentFactory
{
    private readonly AgentDefinitionRegistry _definitions;
    private readonly AgentOrchestrator _orchestrator;

    public AgentFactory(AgentDefinitionRegistry definitions, AgentOrchestrator orchestrator)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(orchestrator);
        _definitions = definitions;
        _orchestrator = orchestrator;
    }

    public IAgent GetAgent(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var (canonicalName, definition) = _definitions.GetRequired(name);
        return new Agent(canonicalName, definition, _orchestrator);
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

        agent = new Agent(canonicalName, definition, _orchestrator);
        return true;
    }
}
