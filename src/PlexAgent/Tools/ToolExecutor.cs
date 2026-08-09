using System.Text.Json;
using PlexAgent.Abstractions;
using PlexAgent.Exceptions;
using PlexAgent.Models;

namespace PlexAgent.Tools;

internal interface IToolExecutor
{
    Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken cancellationToken = default);
}

internal sealed class ToolExecutor : IToolExecutor
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ToolRegistry _registry;

    public ToolExecutor(ToolRegistry registry)
    {
        _registry = registry;
    }

    public async Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        var tool = _registry.GetRequired(call.Name);

        JsonElement args;
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson);
            args = document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new ToolExecutionException(
                call.Name,
                $"Tool '{call.Name}' received invalid JSON arguments.",
                ex);
        }

        ToolArgumentValidator.Validate(tool.ParameterSchema, args, call.Name);

        try
        {
            var result = await tool.InvokeAsync(args, cancellationToken).ConfigureAwait(false);
            return new ToolResult
            {
                ToolCallId = call.Id,
                Name = call.Name,
                Content = SerializeResult(result),
                IsError = false
            };
        }
        catch (ToolExecutionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ToolExecutionException(
                call.Name,
                $"Tool '{call.Name}' failed during execution.",
                ex);
        }
    }

    private static string SerializeResult(object? result)
    {
        if (result is null)
        {
            return "null";
        }

        if (result is string s)
        {
            return s;
        }

        return JsonSerializer.Serialize(result, SerializerOptions);
    }
}
