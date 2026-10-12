using Hatband.Plugins.Steam.Models;

namespace Hatband.Plugins.Steam.Abstractions;

/// <summary>
/// Locates Steam installations and their library folders on the host system.
/// </summary>
public interface ISteamInstallationService
{
    Task<IReadOnlyList<SteamInstallation>> GetInstallationsAsync(CancellationToken cancellationToken = default);
}
