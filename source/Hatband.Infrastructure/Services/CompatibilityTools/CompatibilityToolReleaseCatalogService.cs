using System.Text.Json;

namespace Hatband.Infrastructure.Services.CompatibilityTools;

public sealed class CompatibilityToolReleaseCatalogService : ICompatibilityToolReleaseCatalogService
{
    private readonly IReadOnlyList<ICompatibilityToolReleaseProvider> _releaseProviders;

    public CompatibilityToolReleaseCatalogService(IEnumerable<ICompatibilityToolReleaseProvider> releaseProviders)
    {
        ArgumentNullException.ThrowIfNull(releaseProviders);

        _releaseProviders = releaseProviders
            .OrderBy(provider => provider.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        EnsureReleaseProviderIdsAreUnique(_releaseProviders);
    }

    public IReadOnlyList<CompatibilityToolReleaseProviderInfo> GetProviders()
    {
        return _releaseProviders
            .Select(provider => new CompatibilityToolReleaseProviderInfo(provider.Id, provider.DisplayName))
            .ToArray();
    }

    public async Task<CompatibilityToolReleaseCatalog> GetCatalogAsync(
        string providerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        var provider = _releaseProviders.FirstOrDefault(candidate => candidate.Id == providerId)
            ?? throw new ArgumentException($"Unknown Proton release provider '{providerId}'.", nameof(providerId));

        try
        {
            var releases = await provider.GetLatestReleasesAsync(cancellationToken);
            return new CompatibilityToolReleaseCatalog(provider.Id, provider.DisplayName, releases, null);
        }
        catch (HttpRequestException exception)
        {
            return new CompatibilityToolReleaseCatalog(provider.Id, provider.DisplayName, [], exception.Message);
        }
        catch (JsonException exception)
        {
            return new CompatibilityToolReleaseCatalog(provider.Id, provider.DisplayName, [], exception.Message);
        }
    }

    private static void EnsureReleaseProviderIdsAreUnique(IReadOnlyList<ICompatibilityToolReleaseProvider> providers)
    {
        var duplicateId = providers
            .GroupBy(provider => provider.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateId is not null)
        {
            throw new InvalidOperationException($"The Proton release provider id '{duplicateId.Key}' is registered more than once.");
        }
    }
}
