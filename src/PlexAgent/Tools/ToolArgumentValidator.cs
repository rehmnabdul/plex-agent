using System.Text.Json;
using PlexAgent.Exceptions;

namespace PlexAgent.Tools;

internal static class ToolArgumentValidator
{
    public static void Validate(JsonDocument parameterSchema, JsonElement args, string toolName)
    {
        ArgumentNullException.ThrowIfNull(parameterSchema);

        if (args.ValueKind is not JsonValueKind.Object and not JsonValueKind.Null)
        {
            throw new ToolExecutionException(
                toolName,
                $"Tool '{toolName}' arguments must be a JSON object.");
        }

        var schema = parameterSchema.RootElement;
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!schema.TryGetProperty("required", out var required) || required.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var requiredProperty in required.EnumerateArray())
        {
            var name = requiredProperty.GetString();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out _))
            {
                throw new ToolExecutionException(
                    toolName,
                    $"Tool '{toolName}' is missing required argument '{name}'.");
            }
        }
    }
}
