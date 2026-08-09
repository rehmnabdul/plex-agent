using PlexAgent.Abstractions;
using PlexAgent.Exceptions;
using PlexAgent.Models;

namespace PlexAgent.StructuredOutput;

internal static class StructuredOutputPlanner
{
    /// <summary>
    /// Applies capability-aware structured-output settings for <typeparamref name="T"/>.
    /// Prefers JSON Schema when supported; falls back to JSON object + post-validation.
    /// </summary>
    public static void ApplyForType<T>(
        AgentRequestOptions requestOptions,
        ProviderCapabilities capabilities,
        string providerId,
        string model)
    {
        ArgumentNullException.ThrowIfNull(requestOptions);
        ArgumentNullException.ThrowIfNull(capabilities);

        if (requestOptions.JsonSchema is null
            && requestOptions.ResponseFormat is ResponseFormatKind.Text or ResponseFormatKind.JsonObject)
        {
            requestOptions.JsonSchema = JsonSchemaGenerator.FromType<T>();
        }

        if (requestOptions.JsonSchema is not null || requestOptions.ResponseFormat == ResponseFormatKind.JsonSchema)
        {
            requestOptions.JsonSchema ??= JsonSchemaGenerator.FromType<T>();

            if (capabilities.SupportsJsonSchema)
            {
                requestOptions.ResponseFormat = ResponseFormatKind.JsonSchema;
                return;
            }

            if (capabilities.SupportsJsonObject)
            {
                // Keep schema for post-response validation, but ask the provider for JSON object mode.
                requestOptions.ResponseFormat = ResponseFormatKind.JsonObject;
                return;
            }

            throw new StructuredOutputNotSupportedException(providerId, model);
        }

        if (requestOptions.ResponseFormat == ResponseFormatKind.JsonObject)
        {
            if (!capabilities.SupportsJsonObject && !capabilities.SupportsJsonSchema)
            {
                throw new StructuredOutputNotSupportedException(providerId, model);
            }

            if (!capabilities.SupportsJsonObject && capabilities.SupportsJsonSchema)
            {
                requestOptions.JsonSchema ??= JsonSchemaGenerator.FromType<T>();
                requestOptions.ResponseFormat = ResponseFormatKind.JsonSchema;
            }
        }
    }

    /// <summary>
    /// Downgrades explicit JSON Schema requests when the provider only supports JSON object mode.
    /// </summary>
    public static void ApplyExplicitFormat(
        AgentRequestOptions requestOptions,
        ProviderCapabilities capabilities,
        string providerId,
        string model)
    {
        ArgumentNullException.ThrowIfNull(requestOptions);
        ArgumentNullException.ThrowIfNull(capabilities);

        if (requestOptions.JsonSchema is null && requestOptions.ResponseFormat != ResponseFormatKind.JsonSchema)
        {
            return;
        }

        requestOptions.ResponseFormat = ResponseFormatKind.JsonSchema;

        if (capabilities.SupportsJsonSchema)
        {
            return;
        }

        if (capabilities.SupportsJsonObject)
        {
            requestOptions.ResponseFormat = ResponseFormatKind.JsonObject;
            return;
        }

        throw new StructuredOutputNotSupportedException(providerId, model);
    }
}
