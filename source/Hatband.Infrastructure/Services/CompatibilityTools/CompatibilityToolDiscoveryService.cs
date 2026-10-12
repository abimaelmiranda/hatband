using Hatband.Core.Abstractions.Compatibility;
using Hatband.Core.Abstractions.Host;
using Hatband.Core.Enums.Host;
using Hatband.Core.Models.Games;

namespace Hatband.Infrastructure.Services.CompatibilityTools;

public sealed class CompatibilityToolDiscoveryService : ICompatibilityToolDiscoveryService
{
    private readonly IReadOnlyList<ICompatibilityToolDiscoveryProvider> discoveryProviders;
    private readonly HostOperatingSystem host;

    public CompatibilityToolDiscoveryService(
        IHostSystemInfo hostSystemInfo,
        IEnumerable<ICompatibilityToolDiscoveryProvider> discoveryProviders)
    {
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        ArgumentNullException.ThrowIfNull(discoveryProviders);
        host = hostSystemInfo.Platform;
        this.discoveryProviders = discoveryProviders.ToArray();
        EnsureProviderIdsAreUnique(this.discoveryProviders);
    }

    public async Task<IReadOnlyList<CompatibilityTool>> DiscoverInstalledToolsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var installedTools = new List<CompatibilityTool>();
        foreach (var provider in discoveryProviders.Where(provider => provider.SupportsHost(host)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            installedTools.AddRange(await provider.DiscoverInstalledToolsAsync(cancellationToken));
        }

        return installedTools
            .OrderBy(tool => tool.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static void EnsureProviderIdsAreUnique(IReadOnlyList<ICompatibilityToolDiscoveryProvider> providers)
    {
        foreach (var provider in providers)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(provider.Id);
        }

        var duplicateId = providers
            .GroupBy(provider => provider.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateId is not null)
        {
            throw new InvalidOperationException(
                $"The compatibility tool discovery provider id '{duplicateId.Key}' is registered more than once.");
        }
    }
}
