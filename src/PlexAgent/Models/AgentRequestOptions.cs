using PlexAgent.StructuredOutput;

namespace PlexAgent.Models;

/// <summary>Per-call overrides applied on top of agent defaults.</summary>
public sealed class AgentRequestOptions
{
    /// <summary>Optional provider override for this call.</summary>
    public string? ProviderId { get; set; }

    /// <summary>Optional model override for this call.</summary>
    public string? Model { get; set; }

    /// <summary>Optional temperature override.</summary>
    public float? Temperature { get; set; }

    /// <summary>Optional max tokens override.</summary>
    public int? MaxTokens { get; set; }

    /// <summary>Optional top-p override.</summary>
    public float? TopP { get; set; }

    /// <summary>Optional stop sequences.</summary>
    public IReadOnlyList<string>? StopSequences { get; set; }

    /// <summary>Requested response format for this call.</summary>
    public ResponseFormatKind ResponseFormat { get; set; } = ResponseFormatKind.Text;

    /// <summary>Optional JSON Schema when <see cref="ResponseFormat"/> is <see cref="ResponseFormatKind.JsonSchema"/>.</summary>
    public JsonSchemaResponseFormat? JsonSchema { get; set; }

    /// <summary>Overrides provider and model for this call.</summary>
    public AgentRequestOptions WithProvider(string providerId, string model)
    {
        ProviderId = providerId;
        Model = model;
        return this;
    }

    /// <summary>Overrides only the model for this call.</summary>
    public AgentRequestOptions WithModel(string model)
    {
        Model = model;
        return this;
    }

    /// <summary>Overrides temperature for this call.</summary>
    public AgentRequestOptions WithTemperature(float temperature)
    {
        Temperature = temperature;
        return this;
    }

    /// <summary>Requests JSON object mode without a fixed schema.</summary>
    public AgentRequestOptions WithJsonObject()
    {
        ResponseFormat = ResponseFormatKind.JsonObject;
        JsonSchema = null;
        return this;
    }

    /// <summary>Requests JSON Schema structured output.</summary>
    public AgentRequestOptions WithJsonSchema(JsonSchemaResponseFormat schema)
    {
        ResponseFormat = ResponseFormatKind.JsonSchema;
        JsonSchema = schema;
        return this;
    }

    /// <summary>Uses a reflection-generated JSON Schema for <typeparamref name="T"/>.</summary>
    public AgentRequestOptions WithJsonSchemaFrom<T>(string? name = null, bool strict = true)
        => WithJsonSchema(JsonSchemaGenerator.FromType<T>(name, strict));
}
