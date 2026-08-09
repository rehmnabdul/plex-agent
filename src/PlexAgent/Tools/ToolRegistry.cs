using PlexAgent.Abstractions;
using PlexAgent.Exceptions;

namespace PlexAgent.Tools;

internal sealed class ToolRegistry
{
    private readonly Dictionary<string, IToolDefinition> _tools;

    public ToolRegistry(IEnumerable<IToolDefinition> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        _tools = tools.ToDictionary(static t => t.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IToolDefinition GetRequired(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!_tools.TryGetValue(name, out var tool))
        {
            throw new ToolNotFoundException(name);
        }

        return tool;
    }

    public bool TryGet(string name, out IToolDefinition? tool) =>
        _tools.TryGetValue(name, out tool);

    public IReadOnlyList<IToolDefinition> ResolveMany(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        var resolved = new List<IToolDefinition>();
        foreach (var name in names)
        {
            resolved.Add(GetRequired(name));
        }

        return resolved;
    }
}
