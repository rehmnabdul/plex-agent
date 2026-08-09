using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using PlexAgent.Abstractions;
using PlexAgent.Configuration;
using PlexAgent.DependencyInjection;
using PlexAgent.Models;

namespace PlexAgent.Providers.OpenAI;

/// <summary>OpenAI-specific options.</summary>
public sealed class OpenAiOptions : ProviderOptionsBase
{
    /// <summary>Optional OpenAI organization id.</summary>
    public string? OrganizationId { get; set; }
}

/// <summary>DI extensions for the OpenAI provider adapter.</summary>
public static class OpenAiServiceCollectionExtensions
{
    /// <summary>Registers the OpenAI <c>ILlmProvider</c> adapter and resilient HTTP client.</summary>
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

        RegisterOpenAiServices(builder.Services);
        return builder;
    }

    /// <summary>Registers the OpenAI adapter using a configuration section (e.g. <c>PlexAgent:Providers:OpenAI</c>).</summary>
    public static IPlexAgentBuilder AddOpenAI(this IPlexAgentBuilder builder, IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(section);

        builder.Services.Configure<OpenAiOptions>(section);
        RegisterOpenAiServices(builder.Services);
        return builder;
    }

    private static void RegisterOpenAiServices(IServiceCollection services)
    {
        services.AddHttpClient(OpenAiDefaults.HttpClientName, (sp, client) =>
            {
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenAiOptions>>().Value;
                var timeoutSeconds = options.TimeoutSeconds <= 0 ? 120 : options.TimeoutSeconds;
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            })
            .AddStandardResilienceHandler();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILlmProvider, OpenAiLlmProvider>());
        _ = LlmProviderIds.OpenAI;
    }
}
