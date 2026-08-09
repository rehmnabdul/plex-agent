using Microsoft.Extensions.DependencyInjection;
using PlexAgent.Configuration;
using PlexAgent.DependencyInjection;
using PlexAgent.Models;

namespace PlexAgent.Providers.OpenAI;

/// <summary>OpenAI-specific options.</summary>
public sealed class OpenAiOptions : ProviderOptionsBase
{
    public string? OrganizationId { get; set; }
}

/// <summary>DI extensions for the OpenAI provider adapter.</summary>
public static class OpenAiServiceCollectionExtensions
{
    /// <summary>Registers the OpenAI <c>ILlmProvider</c> adapter. Implementation arrives in Phase 2.</summary>
    public static IPlexAgentBuilder AddOpenAI(this IPlexAgentBuilder builder, Action<OpenAiOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (configure is not null)
        {
            builder.Services.Configure(configure);
        }
        else
        {
            builder.Services.Configure<OpenAiOptions>(_ => { });
        }

        // Provider implementation registration lands in Phase 2.
        _ = LlmProviderIds.OpenAI;
        return builder;
    }
}
