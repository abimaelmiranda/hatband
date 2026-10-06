using Hatband.Core.Models.Compatibility;

namespace Hatband.Core.Abstractions.Compatibility;

public interface ICompatibilityToolReleaseCatalogService
{
    IReadOnlyList<CompatibilityToolReleaseProviderInfo> GetProviders();

    Task<CompatibilityToolReleaseCatalog> GetCatalogAsync(
        string providerId,
        CancellationToken cancellationToken = default);
}
