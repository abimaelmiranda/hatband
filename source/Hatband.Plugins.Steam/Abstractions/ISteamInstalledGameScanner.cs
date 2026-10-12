using Hatband.Plugins.Steam.Models;

namespace Hatband.Plugins.Steam.Abstractions;

/// <summary>
/// Finds Steam games installed in local Steam library folders.
/// </summary>
public interface ISteamInstalledGameScanner
{
    Task<IReadOnlyList<SteamLibraryGame>> ScanAsync(CancellationToken cancellationToken = default);
}
