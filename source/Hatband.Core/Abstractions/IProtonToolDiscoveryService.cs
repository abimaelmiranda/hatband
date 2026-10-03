using Hatband.Core.Models;

namespace Hatband.Core.Abstractions;

public interface IProtonToolDiscoveryService
{
    Task<IReadOnlyList<ProtonTool>> DiscoverInstalledToolsAsync(CancellationToken cancellationToken = default);
}
