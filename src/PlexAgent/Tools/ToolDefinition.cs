using System.Text.Json;
using PlexAgent.Abstractions;

namespace PlexAgent.Tools;

/// <summary>Helper factory for code-first tool definitions.</summary>
public static class ToolDefinition
{
    /// <summary>Creates an <see cref="IToolDefinition"/> from delegates.</summary>
    public static IToolDefinition Create(
        string name,
        string description,
        JsonDocument parameterSchema,
        Func<JsonElement, CancellationToken, Task<object?>> invokeAsync)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(parameterSchema);
        ArgumentNullException.ThrowIfNull(invokeAsync);

        return new DelegateToolDefinition(name, description, parameterSchema, invokeAsync);
    }

    private sealed class DelegateToolDefinition : IToolDefinition
    {
        public DelegateToolDefinition(
            string name,
            string description,
            JsonDocument parameterSchema,
            Func<JsonElement, CancellationToken, Task<object?>> invokeAsync)
        {
            Name = name;
            Description = description;
            ParameterSchema = parameterSchema;
            InvokeAsync = invokeAsync;
        }

        public string Name { get; }

        public string Description { get; }

        public JsonDocument ParameterSchema { get; }

        public Func<JsonElement, CancellationToken, Task<object?>> InvokeAsync { get; }
    }
}
