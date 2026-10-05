using System.Text.Json;
namespace Hatband.Infrastructure.Services.CompatibilityTools;

public sealed class CompatibilityToolReleaseCatalogService : ICompatibilityToolReleaseCatalogService
{
    private readonly IReadOnlyList<ICompatibilityToolReleaseProvider> releaseProviders;

    public CompatibilityToolReleaseCatalogService(IEnumerable<ICompatibilityToolReleaseProvider> releaseProviders)
    {
        ArgumentNullException.ThrowIfNull(releaseProviders);

        this.releaseProviders = releaseProviders
            .OrderBy(provider => provider.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        EnsureReleaseProviderIdsAreUnique(this.releaseProviders);
    }

    public async Task<IReadOnlyList<CompatibilityToolReleaseCatalog>> GetCatalogsAsync(
        CancellationToken cancellationToken = default)
    {
        var catalogs = new List<CompatibilityToolReleaseCatalog>(releaseProviders.Count);

        foreach (var provider in releaseProviders)
        {
            try
            {
                var releases = await provider.GetLatestReleasesAsync(cancellationToken);
                catalogs.Add(new CompatibilityToolReleaseCatalog(provider.Id, provider.DisplayName, releases, null));
            }
            catch (HttpRequestException exception)
            {
                catalogs.Add(new CompatibilityToolReleaseCatalog(provider.Id, provider.DisplayName, [], exception.Message));
            }
            catch (JsonException exception)
            {
                catalogs.Add(new CompatibilityToolReleaseCatalog(provider.Id, provider.DisplayName, [], exception.Message));
            }
        }

        return catalogs;
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
