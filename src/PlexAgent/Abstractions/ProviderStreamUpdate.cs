using PlexAgent.Models;

namespace PlexAgent.Abstractions;

/// <summary>Incremental update from a provider stream.</summary>
public sealed class ProviderStreamUpdate
{
    /// <summary>Optional assistant text delta.</summary>
    public string? TextDelta { get; init; }

    /// <summary>
    /// When set, the provider stream has finished this completion and the aggregated result is available.
    /// </summary>
    public ProviderCompletionResult? Completed { get; init; }
}
