using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using PlexAgent.Abstractions;
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
    /// <summary>Registers the Anthropic <c>ILlmProvider</c> adapter and resilient HTTP client.</summary>
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

        RegisterServices(builder.Services);
        return builder;
    }

    /// <summary>Registers the Anthropic adapter using a configuration section.</summary>
    public static IPlexAgentBuilder AddAnthropic(this IPlexAgentBuilder builder, IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(section);

        builder.Services.Configure<AnthropicOptions>(section);
        RegisterServices(builder.Services);
        return builder;
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddHttpClient(AnthropicDefaults.HttpClientName, (sp, client) =>
            {
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AnthropicOptions>>().Value;
                var timeoutSeconds = options.TimeoutSeconds <= 0 ? 120 : options.TimeoutSeconds;
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            })
            .AddStandardResilienceHandler();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILlmProvider, AnthropicLlmProvider>());
        _ = LlmProviderIds.Anthropic;
    }
}
