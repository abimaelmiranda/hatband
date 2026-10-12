using Hatband.Core.Abstractions.Compatibility;
using Hatband.Core.Models.Compatibility;

namespace Hatband.Infrastructure.Services.CompatibilityTools;

public sealed class CompatibilityToolInstallationService : ICompatibilityToolInstallationService
{
    private readonly IReadOnlyDictionary<string, ICompatibilityToolInstallationProvider> providersByReleaseProviderId;

    public CompatibilityToolInstallationService(IEnumerable<ICompatibilityToolInstallationProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var providerList = providers.ToArray();
        foreach (var provider in providerList)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(provider.Id);
            foreach (var releaseProviderId in provider.ReleaseProviderIds)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(releaseProviderId);
            }
        }

        var duplicateProviderId = providerList
            .GroupBy(provider => provider.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateProviderId is not null)
        {
            throw new InvalidOperationException(
                $"The compatibility tool installation provider id '{duplicateProviderId.Key}' is registered more than once.");
        }

        var providerMappings = new Dictionary<string, ICompatibilityToolInstallationProvider>(StringComparer.Ordinal);
        foreach (var provider in providerList)
        {
            foreach (var releaseProviderId in provider.ReleaseProviderIds)
            {
                if (!providerMappings.TryAdd(releaseProviderId, provider))
                {
                    throw new InvalidOperationException(
                        $"The compatibility tool release provider id '{releaseProviderId}' is registered more than once for installation.");
                }
            }
        }

        providersByReleaseProviderId = providerMappings;
    }

    public Task InstallAsync(CompatibilityToolRelease release, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentException.ThrowIfNullOrWhiteSpace(release.ProviderId);

        if (!providersByReleaseProviderId.TryGetValue(release.ProviderId, out var provider))
        {
            throw new ArgumentException(
                $"No compatibility tool installation provider is registered for release provider '{release.ProviderId}'.",
                nameof(release));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return provider.InstallAsync(release, cancellationToken);
    }
}
