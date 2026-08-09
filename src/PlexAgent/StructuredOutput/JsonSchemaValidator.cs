using System.Text.Json;
using PlexAgent.Exceptions;

namespace PlexAgent.StructuredOutput;

/// <summary>
/// Lightweight JSON Schema validator covering type, required, properties, items,
/// enum, additionalProperties, and common numeric/string/array bounds.
/// </summary>
internal static class JsonSchemaValidator
{
    public static void ValidateStructured(JsonDocument schema, string json, string typeName)
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

        try
        {
            Validate(schema.RootElement, payload, "$");
        }
        catch (JsonSchemaValidationException ex)
        {
            throw new StructuredOutputException(
                $"Structured response for '{typeName}' failed schema validation: {ex.Message}",
                json,
                ex);
        }
    }

    public static void ValidateToolArguments(JsonDocument schema, JsonElement args, string toolName)
    {
        ArgumentNullException.ThrowIfNull(schema);

        try
        {
            Validate(schema.RootElement, args, "$");
        }
        catch (JsonSchemaValidationException ex)
        {
            throw new ToolExecutionException(
                toolName,
                $"Tool '{toolName}' arguments failed schema validation: {ex.Message}",
                ex);
        }
    }

    /// <summary>Kept for callers/tests that only need required-property checks via full validation.</summary>
    public static void ValidateRequired(JsonDocument schema, string json, string typeName)
        => ValidateStructured(schema, json, typeName);

    public static void Validate(JsonElement schema, JsonElement value, string path)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (schema.TryGetProperty("enum", out var enumValues) && enumValues.ValueKind == JsonValueKind.Array)
        {
            var matched = false;
            foreach (var allowed in enumValues.EnumerateArray())
            {
                if (JsonElementEquals(allowed, value))
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                throw new JsonSchemaValidationException($"{path} must be one of the enum values.");
            }
        }

        if (schema.TryGetProperty("type", out var typeNode))
        {
            EnsureType(typeNode, value, path);
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                ValidateObject(schema, value, path);
                break;
            case JsonValueKind.Array:
                ValidateArray(schema, value, path);
                break;
            case JsonValueKind.String:
                ValidateString(schema, value, path);
                break;
            case JsonValueKind.Number:
                ValidateNumber(schema, value, path);
                break;
        }
    }

    private static void ValidateObject(JsonElement schema, JsonElement value, string path)
    {
        if (schema.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array)
        {
            foreach (var requiredProperty in required.EnumerateArray())
            {
                var name = requiredProperty.GetString();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (!value.TryGetProperty(name, out _))
                {
                    throw new JsonSchemaValidationException($"{path} is missing required property '{name}'.");
                }
            }
        }

        JsonElement? properties = schema.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object
            ? props
            : null;

        var additionalAllowed = true;
        JsonElement? additionalSchema = null;
        if (schema.TryGetProperty("additionalProperties", out var additional))
        {
            if (additional.ValueKind == JsonValueKind.False)
            {
                additionalAllowed = false;
            }
            else if (additional.ValueKind == JsonValueKind.Object)
            {
                additionalSchema = additional;
            }
        }

        foreach (var property in value.EnumerateObject())
        {
            var childPath = $"{path}.{property.Name}";
            if (properties is JsonElement propertyMap && propertyMap.TryGetProperty(property.Name, out var propertySchema))
            {
                Validate(propertySchema, property.Value, childPath);
                continue;
            }

            if (!additionalAllowed)
            {
                throw new JsonSchemaValidationException($"{path} does not allow additional property '{property.Name}'.");
            }

            if (additionalSchema is JsonElement addSchema)
            {
                Validate(addSchema, property.Value, childPath);
            }
        }
    }

    private static void ValidateArray(JsonElement schema, JsonElement value, string path)
    {
        if (schema.TryGetProperty("minItems", out var minItems) && minItems.TryGetInt32(out var min) && value.GetArrayLength() < min)
        {
            throw new JsonSchemaValidationException($"{path} must have at least {min} items.");
        }

        if (schema.TryGetProperty("maxItems", out var maxItems) && maxItems.TryGetInt32(out var max) && value.GetArrayLength() > max)
        {
            throw new JsonSchemaValidationException($"{path} must have at most {max} items.");
        }

        if (schema.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Object)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                Validate(items, item, $"{path}[{index}]");
                index++;
            }
        }
    }

    private static void ValidateString(JsonElement schema, JsonElement value, string path)
    {
        var text = value.GetString() ?? string.Empty;
        if (schema.TryGetProperty("minLength", out var minLength) && minLength.TryGetInt32(out var min) && text.Length < min)
        {
            throw new JsonSchemaValidationException($"{path} must be at least {min} characters.");
        }

        if (schema.TryGetProperty("maxLength", out var maxLength) && maxLength.TryGetInt32(out var max) && text.Length > max)
        {
            throw new JsonSchemaValidationException($"{path} must be at most {max} characters.");
        }
    }

    private static void ValidateNumber(JsonElement schema, JsonElement value, string path)
    {
        var number = value.GetDouble();
        if (schema.TryGetProperty("minimum", out var minimum) && minimum.TryGetDouble(out var min) && number < min)
        {
            throw new JsonSchemaValidationException($"{path} must be >= {min}.");
        }

        if (schema.TryGetProperty("maximum", out var maximum) && maximum.TryGetDouble(out var max) && number > max)
        {
            throw new JsonSchemaValidationException($"{path} must be <= {max}.");
        }
    }

    private static void EnsureType(JsonElement typeNode, JsonElement value, string path)
    {
        if (typeNode.ValueKind == JsonValueKind.Array)
        {
            foreach (var option in typeNode.EnumerateArray())
            {
                if (option.ValueKind == JsonValueKind.String && MatchesType(option.GetString(), value))
                {
                    return;
                }
            }

            throw new JsonSchemaValidationException($"{path} does not match any allowed schema type.");
        }

        if (typeNode.ValueKind == JsonValueKind.String && !MatchesType(typeNode.GetString(), value))
        {
            throw new JsonSchemaValidationException($"{path} must be of type '{typeNode.GetString()}'.");
        }
    }

    private static bool MatchesType(string? type, JsonElement value) =>
        type switch
        {
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            "string" => value.ValueKind == JsonValueKind.String,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "null" => value.ValueKind == JsonValueKind.Null,
            "number" => value.ValueKind == JsonValueKind.Number,
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            _ => true
        };

    private static bool JsonElementEquals(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        return left.ValueKind switch
        {
            JsonValueKind.String => left.GetString() == right.GetString(),
            JsonValueKind.Number => left.GetRawText() == right.GetRawText(),
            JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => true,
            _ => left.GetRawText() == right.GetRawText()
        };
    }
}

internal sealed class JsonSchemaValidationException : Exception
{
    public JsonSchemaValidationException(string message)
        : base(message)
    {
    }
}
