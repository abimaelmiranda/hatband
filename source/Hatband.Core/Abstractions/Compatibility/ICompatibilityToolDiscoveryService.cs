using Hatband.Core.Models.Games;

namespace Hatband.Core.Abstractions.Compatibility;

/// <summary>
/// Finds installed compatibility runtimes that can be selected for games.
/// </summary>
public interface ICompatibilityToolDiscoveryService
{
    Task<IReadOnlyList<CompatibilityTool>> DiscoverInstalledToolsAsync(
        CancellationToken cancellationToken = default);
}
