using System.Text.Json;

namespace PlexAgent.Abstractions;

/// <summary>Definition of a tool/function that an agent can invoke.</summary>
public interface IToolDefinition
{
    /// <summary>Unique tool name.</summary>
    string Name { get; }

    /// <summary>Human-readable description for the model.</summary>
    string Description { get; }

    /// <summary>JSON Schema describing tool parameters.</summary>
    JsonDocument ParameterSchema { get; }

    /// <summary>Invokes the tool with JSON arguments.</summary>
    Func<JsonElement, CancellationToken, Task<object?>> InvokeAsync { get; }
}
