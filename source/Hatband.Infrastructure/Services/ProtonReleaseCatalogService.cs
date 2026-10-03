using System.Text.Json;
using Hatband.Core.Abstractions;
using Hatband.Core.Models;

namespace Hatband.Infrastructure.Services;

public sealed class ProtonReleaseCatalogService : IProtonReleaseCatalogService
{
    private readonly IReadOnlyList<IProtonReleaseProvider> releaseProviders;

    public ProtonReleaseCatalogService(IEnumerable<IProtonReleaseProvider> releaseProviders)
    {
        ArgumentNullException.ThrowIfNull(releaseProviders);

        this.releaseProviders = releaseProviders
            .OrderBy(provider => provider.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        EnsureReleaseProviderIdsAreUnique(this.releaseProviders);
    }

    public async Task<IReadOnlyList<ProtonReleaseCatalog>> GetCatalogsAsync(
        CancellationToken cancellationToken = default)
    {
        var catalogs = new List<ProtonReleaseCatalog>(releaseProviders.Count);

        foreach (var provider in releaseProviders)
        {
            try
            {
                var releases = await provider.GetLatestReleasesAsync(cancellationToken);
                catalogs.Add(new ProtonReleaseCatalog(provider.Id, provider.DisplayName, releases, null));
            }
            catch (HttpRequestException exception)
            {
                catalogs.Add(new ProtonReleaseCatalog(provider.Id, provider.DisplayName, [], exception.Message));
            }
            catch (JsonException exception)
            {
                catalogs.Add(new ProtonReleaseCatalog(provider.Id, provider.DisplayName, [], exception.Message));
            }
        }

        return catalogs;
    }

    private static void EnsureReleaseProviderIdsAreUnique(IReadOnlyList<IProtonReleaseProvider> providers)
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
