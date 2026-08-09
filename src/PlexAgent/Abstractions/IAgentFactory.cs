namespace PlexAgent.Abstractions;

/// <summary>Resolves configured agents by name.</summary>
public interface IAgentFactory
{
    /// <summary>Gets a registered agent or throws when missing.</summary>
    IAgent GetAgent(string name);

    /// <summary>Tries to get a registered agent by name.</summary>
    bool TryGetAgent(string name, out IAgent? agent);
}
