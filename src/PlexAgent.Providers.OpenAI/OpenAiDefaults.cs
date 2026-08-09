namespace PlexAgent.Providers.OpenAI;

/// <summary>Shared constants for the OpenAI provider package.</summary>
public static class OpenAiDefaults
{
    /// <summary>Named <see cref="System.Net.Http.HttpClient"/> used by the OpenAI adapter.</summary>
    public const string HttpClientName = "PlexAgent.OpenAI";

    /// <summary>Default OpenAI API base URL.</summary>
    public const string DefaultBaseUrl = "https://api.openai.com/v1";
}
