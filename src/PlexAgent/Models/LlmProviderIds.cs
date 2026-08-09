namespace PlexAgent.Models;

/// <summary>Well-known provider id constants. Prefer strings for third-party extensibility.</summary>
public static class LlmProviderIds
{
    /// <summary>OpenAI provider id.</summary>
    public const string OpenAI = "OpenAI";

    /// <summary>Anthropic provider id.</summary>
    public const string Anthropic = "Anthropic";

    /// <summary>Google Gemini provider id.</summary>
    public const string Gemini = "Gemini";
}
