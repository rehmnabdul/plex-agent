using Microsoft.Extensions.Options;
using PlexAgent.Configuration;
using PlexAgent.Exceptions;

namespace PlexAgent.Internal;

internal sealed class AgentDefinitionRegistry
{
    private readonly PlexAgentOptions _options;

    public AgentDefinitionRegistry(IOptions<PlexAgentOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public (string Name, AgentDefinitionOptions Definition) GetRequired(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        foreach (var pair in _options.Agents)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return (pair.Key, pair.Value);
            }
        }

        throw new AgentNotFoundException(name);
    }

    public bool TryGet(string name, out string? canonicalName, out AgentDefinitionOptions? definition)
    {
        canonicalName = null;
        definition = null;

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        foreach (var pair in _options.Agents)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                canonicalName = pair.Key;
                definition = pair.Value;
                return true;
            }
        }

        return false;
    }
}
