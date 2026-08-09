using Microsoft.Extensions.DependencyInjection;
using PlexAgent.Configuration;
using PlexAgent.DependencyInjection;
using PlexAgent.Models;

namespace PlexAgent.Providers.Gemini;

/// <summary>Gemini-specific options.</summary>
public sealed class GeminiOptions : ProviderOptionsBase
{
}

/// <summary>DI extensions for the Gemini provider adapter.</summary>
public static class GeminiServiceCollectionExtensions
{
    /// <summary>Registers the Gemini <c>ILlmProvider</c> adapter. Implementation arrives in Phase 3.</summary>
    public static IPlexAgentBuilder AddGemini(this IPlexAgentBuilder builder, Action<GeminiOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (configure is not null)
        {
            builder.Services.Configure(configure);
        }
        else
        {
            builder.Services.Configure<GeminiOptions>(_ => { });
        }

        _ = LlmProviderIds.Gemini;
        return builder;
    }
}
