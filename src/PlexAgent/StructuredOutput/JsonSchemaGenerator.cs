using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using PlexAgent.Models;

namespace PlexAgent.StructuredOutput;

/// <summary>Reflection-based JSON Schema generation for structured responses.</summary>
public static class JsonSchemaGenerator
{
    private static readonly NullabilityInfoContext Nullability = new();

    /// <summary>Builds a <see cref="JsonSchemaResponseFormat"/> from <typeparamref name="T"/>.</summary>
    public static JsonSchemaResponseFormat FromType<T>(string? name = null, bool strict = true)
        => FromType(typeof(T), name, strict);

    /// <summary>Builds a <see cref="JsonSchemaResponseFormat"/> from a CLR type.</summary>
    public static JsonSchemaResponseFormat FromType(Type type, string? name = null, bool strict = true)
    {
        ArgumentNullException.ThrowIfNull(type);

        var schema = BuildSchema(type, strict);
        var document = JsonDocument.Parse(schema.ToJsonString());
        return new JsonSchemaResponseFormat
        {
            Name = string.IsNullOrWhiteSpace(name) ? SanitizeName(type.Name) : name,
            Schema = document,
            Strict = strict
        };
    }

    private static JsonObject BuildSchema(Type type, bool strict)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type == typeof(string) || type == typeof(char) || type == typeof(Guid) || type == typeof(Uri))
        {
            return new JsonObject { ["type"] = "string" };
        }

        if (type == typeof(bool))
        {
            return new JsonObject { ["type"] = "boolean" };
        }

        if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort)
            || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong))
        {
            return new JsonObject { ["type"] = "integer" };
        }

        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
        {
            return new JsonObject { ["type"] = "number" };
        }

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly) || type == typeof(TimeOnly))
        {
            return new JsonObject
            {
                ["type"] = "string",
                ["format"] = "date-time"
            };
        }

        if (type.IsEnum)
        {
            var values = new JsonArray();
            foreach (var name in Enum.GetNames(type))
            {
                values.Add(name);
            }

            return new JsonObject
            {
                ["type"] = "string",
                ["enum"] = values
            };
        }

        if (IsDictionary(type, out var valueType))
        {
            return new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = BuildSchema(valueType!, strict)
            };
        }

        if (IsArrayLike(type, out var elementType))
        {
            return new JsonObject
            {
                ["type"] = "array",
                ["items"] = BuildSchema(elementType!, strict)
            };
        }

        var properties = new JsonObject();
        var required = new JsonArray();
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            var jsonName = ToCamelCase(property.Name);
            properties[jsonName] = BuildSchema(property.PropertyType, strict);

            if (IsRequired(property))
            {
                required.Add(jsonName);
            }
        }

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties
        };

        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        if (strict)
        {
            schema["additionalProperties"] = false;
        }

        return schema;
    }

    private static bool IsRequired(PropertyInfo property)
    {
        var underlying = Nullable.GetUnderlyingType(property.PropertyType);
        if (underlying is not null)
        {
            return false;
        }

        if (property.PropertyType.IsValueType)
        {
            return true;
        }

        var info = Nullability.Create(property);
        return info.ReadState != NullabilityState.Nullable;
    }

    private static bool IsArrayLike(Type type, out Type? elementType)
    {
        if (type.IsArray)
        {
            elementType = type.GetElementType();
            return elementType is not null;
        }

        if (type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type))
        {
            if (type.IsGenericType)
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }
        }

        elementType = null;
        return false;
    }

    private static bool IsDictionary(Type type, out Type? valueType)
    {
        foreach (var interfaceType in type.GetInterfaces().Append(type))
        {
            if (interfaceType.IsGenericType
                && (interfaceType.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                    || interfaceType.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)))
            {
                var args = interfaceType.GetGenericArguments();
                if (args[0] == typeof(string))
                {
                    valueType = args[1];
                    return true;
                }
            }
        }

        valueType = null;
        return false;
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
        {
            return name;
        }

        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static string SanitizeName(string name)
    {
        var cleaned = new string(name.Where(static c => char.IsLetterOrDigit(c) || c is '_' or '-').ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "response" : cleaned;
    }
}
