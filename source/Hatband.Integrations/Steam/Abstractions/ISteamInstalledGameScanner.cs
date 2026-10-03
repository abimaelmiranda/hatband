using Hatband.Integrations.Steam.Models;

namespace Hatband.Integrations.Steam.Abstractions;

/// <summary>
/// Finds Steam games installed in local Steam library folders.
/// </summary>
public interface ISteamInstalledGameScanner
{
    Task<IReadOnlyList<SteamLibraryGame>> ScanAsync(CancellationToken cancellationToken = default);
}
