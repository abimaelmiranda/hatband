using Hatband.Integrations.Steam.Models;

namespace Hatband.Integrations.Steam.Abstractions;

/// <summary>
/// Locates Steam installations and their library folders on the host system.
/// </summary>
public interface ISteamInstallationService
{
    Task<IReadOnlyList<SteamInstallation>> GetInstallationsAsync(CancellationToken cancellationToken = default);
}
