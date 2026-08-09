using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using PlexAgent.Abstractions;
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
    /// <summary>Registers the Gemini <c>ILlmProvider</c> adapter and resilient HTTP client.</summary>
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

        RegisterServices(builder.Services);
        return builder;
    }

    /// <summary>Registers the Gemini adapter using a configuration section.</summary>
    public static IPlexAgentBuilder AddGemini(this IPlexAgentBuilder builder, IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(section);

        builder.Services.Configure<GeminiOptions>(section);
        RegisterServices(builder.Services);
        return builder;
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddHttpClient(GeminiDefaults.HttpClientName, (sp, client) =>
            {
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GeminiOptions>>().Value;
                var timeoutSeconds = options.TimeoutSeconds <= 0 ? 120 : options.TimeoutSeconds;
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            })
            .AddStandardResilienceHandler();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILlmProvider, GeminiLlmProvider>());
        _ = LlmProviderIds.Gemini;
    }
}
