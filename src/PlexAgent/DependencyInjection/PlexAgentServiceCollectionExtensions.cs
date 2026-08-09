using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PlexAgent.Abstractions;
using PlexAgent.Agents;
using PlexAgent.Configuration;
using PlexAgent.Internal;
using PlexAgent.Tools;

namespace PlexAgent.DependencyInjection;

/// <summary>Fluent builder returned by <c>AddPlexAgent</c>.</summary>
public interface IPlexAgentBuilder
{
    /// <summary>Underlying service collection.</summary>
    IServiceCollection Services { get; }

    /// <summary>Registers a tool instance for agents to reference by name.</summary>
    IPlexAgentBuilder AddTool(IToolDefinition tool);

    /// <summary>Registers a tool type for agents to reference by name.</summary>
    IPlexAgentBuilder AddTool<TTool>() where TTool : class, IToolDefinition;
}

internal sealed class PlexAgentBuilder : IPlexAgentBuilder
{
    public PlexAgentBuilder(IServiceCollection services)
    {
        Services = services;
    }

    public IServiceCollection Services { get; }

    public IPlexAgentBuilder AddTool(IToolDefinition tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IToolDefinition>(tool));
        return this;
    }

    public IPlexAgentBuilder AddTool<TTool>() where TTool : class, IToolDefinition
    {
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IToolDefinition, TTool>());
        return this;
    }
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
        RegisterCoreServices(services);
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
        RegisterCoreServices(services);
        return new PlexAgentBuilder(services);
    }

    private static void RegisterCoreServices(IServiceCollection services)
    {
        services.AddLogging();
        services.TryAddSingleton<LlmProviderRegistry>();
        services.TryAddSingleton<AgentDefinitionRegistry>();
        services.TryAddSingleton<ToolRegistry>();
        services.TryAddSingleton<IToolExecutor, ToolExecutor>();
        services.TryAddSingleton<AgentOrchestrator>();
        services.TryAddScoped<IAgentFactory, AgentFactory>();
    }
}
