using Hatband.Core.Models.Compatibility;

namespace Hatband.Core.Abstractions.Compatibility;

public interface ICompatibilityToolReleaseCatalogService
{
    Task<IReadOnlyList<CompatibilityToolReleaseCatalog>> GetCatalogsAsync(
        CancellationToken cancellationToken = default);
}
