namespace PlexAgent.Abstractions;

/// <summary>
/// Contract implemented by LLM provider adapters (OpenAI, Anthropic, Gemini, or custom).
/// </summary>
public interface ILlmProvider
{
    /// <summary>Unique provider identifier, e.g. <c>OpenAI</c>.</summary>
    string ProviderId { get; }

    /// <summary>Returns capability flags for the provider/model combination.</summary>
    Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default);

    /// <summary>Runs a non-streaming completion request.</summary>
    Task<ProviderCompletionResult> CompleteAsync(
        ProviderCompletionRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Capability advertisement for a provider adapter.</summary>
/// <param name="SupportsToolCalling">Whether tool/function calling is supported.</param>
/// <param name="SupportsStreaming">Whether streaming is supported (false in v1 adapters).</param>
/// <param name="SupportsSystemMessages">Whether system messages are supported.</param>
/// <param name="SupportsJsonObject">Whether JSON object mode is supported.</param>
/// <param name="SupportsJsonSchema">Whether strict JSON schema responses are supported.</param>
/// <param name="SupportedModels">Known model ids; may be empty when dynamic.</param>
public sealed record ProviderCapabilities(
    bool SupportsToolCalling,
    bool SupportsStreaming,
    bool SupportsSystemMessages,
    bool SupportsJsonObject,
    bool SupportsJsonSchema,
    IReadOnlyList<string> SupportedModels);
