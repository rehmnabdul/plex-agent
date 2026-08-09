using PlexAgent.StructuredOutput;

namespace PlexAgent.Models;

/// <summary>Per-call overrides applied on top of agent defaults.</summary>
public sealed class AgentRequestOptions
{
    public string? ProviderId { get; set; }

    public string? Model { get; set; }

    public float? Temperature { get; set; }

    public int? MaxTokens { get; set; }

    public float? TopP { get; set; }

    public IReadOnlyList<string>? StopSequences { get; set; }

    public ResponseFormatKind ResponseFormat { get; set; } = ResponseFormatKind.Text;

    public JsonSchemaResponseFormat? JsonSchema { get; set; }

    public AgentRequestOptions WithProvider(string providerId, string model)
    {
        ProviderId = providerId;
        Model = model;
        return this;
    }

    public AgentRequestOptions WithModel(string model)
    {
        Model = model;
        return this;
    }

    public AgentRequestOptions WithTemperature(float temperature)
    {
        Temperature = temperature;
        return this;
    }

    public AgentRequestOptions WithJsonObject()
    {
        ResponseFormat = ResponseFormatKind.JsonObject;
        JsonSchema = null;
        return this;
    }

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
