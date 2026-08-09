using Microsoft.Extensions.DependencyInjection;
using PlexAgent.Configuration;
using PlexAgent.DependencyInjection;
using PlexAgent.Models;

namespace PlexAgent.Providers.Anthropic;

/// <summary>Anthropic-specific options.</summary>
public sealed class AnthropicOptions : ProviderOptionsBase
{
}

/// <summary>DI extensions for the Anthropic provider adapter.</summary>
public static class AnthropicServiceCollectionExtensions
{
    /// <summary>Registers the Anthropic <c>ILlmProvider</c> adapter. Implementation arrives in Phase 3.</summary>
    public static IPlexAgentBuilder AddAnthropic(this IPlexAgentBuilder builder, Action<AnthropicOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (configure is not null)
        {
            builder.Services.Configure(configure);
        }
        else
        {
            builder.Services.Configure<AnthropicOptions>(_ => { });
        }

        _ = LlmProviderIds.Anthropic;
        return builder;
    }
}
