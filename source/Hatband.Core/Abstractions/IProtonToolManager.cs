using Hatband.Core.Models;

namespace Hatband.Core.Abstractions;

public interface IProtonToolManager
{
    Task<IReadOnlyList<ProtonTool>> DiscoverInstalledToolsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProtonReleaseCatalog>> GetCatalogsAsync(CancellationToken cancellationToken = default);

    Task InstallAsync(ProtonRelease release, CancellationToken cancellationToken = default);
}
