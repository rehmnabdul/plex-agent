using System.Text.Json;
using PlexAgent.Exceptions;

namespace PlexAgent.StructuredOutput;

internal static class JsonSchemaValidator
{
    /// <summary>
    /// Validates that JSON payload satisfies the schema's <c>required</c> object properties.
    /// Full draft validation is intentionally out of scope for v1.
    /// </summary>
    public static void ValidateRequired(JsonDocument schema, string json, string typeName)
    {
        ArgumentNullException.ThrowIfNull(schema);

        JsonElement payload;
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "null" : json);
            payload = document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new StructuredOutputException(
                $"Structured response for '{typeName}' was not valid JSON.",
                json,
                ex);
        }

        var root = schema.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("required", out var required)
            || required.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        if (payload.ValueKind != JsonValueKind.Object)
        {
            throw new StructuredOutputException(
                $"Structured response for '{typeName}' must be a JSON object.",
                json);
        }

        foreach (var requiredProperty in required.EnumerateArray())
        {
            var name = requiredProperty.GetString();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (!payload.TryGetProperty(name, out _))
            {
                throw new StructuredOutputException(
                    $"Structured response for '{typeName}' is missing required property '{name}'.",
                    json);
            }
        }
    }
}
