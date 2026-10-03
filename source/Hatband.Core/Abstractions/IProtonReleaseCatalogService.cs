using Hatband.Core.Models;

namespace Hatband.Core.Abstractions;

public interface IProtonReleaseCatalogService
{
    Task<IReadOnlyList<ProtonReleaseCatalog>> GetCatalogsAsync(CancellationToken cancellationToken = default);
}
