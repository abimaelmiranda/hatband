using Hatband.Core.Enums.Host;
using Hatband.Core.Models.Games;

namespace Hatband.Core.Abstractions.Compatibility;

/// <summary>Discovers compatibility tools installed by a specific provider.</summary>
public interface ICompatibilityToolDiscoveryProvider
{
    string Id { get; }

    bool SupportsHost(HostOperatingSystem host);

    Task<IReadOnlyList<CompatibilityTool>> DiscoverInstalledToolsAsync(
        CancellationToken cancellationToken = default);
}
