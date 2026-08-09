using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlexAgent.Configuration;

namespace PlexAgent.DependencyInjection;

/// <summary>Fluent builder returned by <c>AddPlexAgent</c>.</summary>
public interface IPlexAgentBuilder
{
    /// <summary>Underlying service collection.</summary>
    IServiceCollection Services { get; }
}

internal sealed class PlexAgentBuilder : IPlexAgentBuilder
{
    public PlexAgentBuilder(IServiceCollection services)
    {
        Services = services;
    }

    public IServiceCollection Services { get; }
}

/// <summary>ASP.NET Core / generic host DI registration for Plex Agent.</summary>
public static class PlexAgentServiceCollectionExtensions
{
    /// <summary>Registers Plex Agent core services and binds options from configuration.</summary>
    public static IPlexAgentBuilder AddPlexAgent(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<PlexAgentOptions>(configuration.GetSection(PlexAgentOptions.SectionName));
        return new PlexAgentBuilder(services);
    }

    /// <summary>Registers Plex Agent core services using an in-code options callback (console/worker apps).</summary>
    public static IPlexAgentBuilder AddPlexAgent(
        this IServiceCollection services,
        Action<PlexAgentOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        return new PlexAgentBuilder(services);
    }
}
