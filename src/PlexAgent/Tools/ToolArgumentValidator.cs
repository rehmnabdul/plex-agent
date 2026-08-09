using System.Text.Json;
using PlexAgent.StructuredOutput;

namespace PlexAgent.Tools;

internal static class ToolArgumentValidator
{
    public static void Validate(JsonDocument parameterSchema, JsonElement args, string toolName)
    {
        ArgumentNullException.ThrowIfNull(parameterSchema);

        if (args.ValueKind is not JsonValueKind.Object and not JsonValueKind.Null)
        {
            throw new Exceptions.ToolExecutionException(
                toolName,
                $"Tool '{toolName}' arguments must be a JSON object.");
        }

        JsonSchemaValidator.ValidateToolArguments(parameterSchema, args, toolName);
    }
}
