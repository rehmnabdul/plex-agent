using PlexAgent.Abstractions;
using PlexAgent.Exceptions;

namespace PlexAgent.Internal;

internal sealed class LlmProviderRegistry
{
    private readonly Dictionary<string, ILlmProvider> _providers;

    public LlmProviderRegistry(IEnumerable<ILlmProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _providers = providers.ToDictionary(
            static p => p.ProviderId,
            StringComparer.OrdinalIgnoreCase);
    }

    public ILlmProvider GetRequired(string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);

        if (!_providers.TryGetValue(providerId, out var provider))
        {
            throw new ProviderNotRegisteredException(providerId);
        }

        return provider;
    }

    public bool TryGet(string providerId, out ILlmProvider? provider) =>
        _providers.TryGetValue(providerId, out provider);
}
