using System.Globalization;
using Hatband.Core.Abstractions;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models;
using Hatband.Integrations.Steam.Abstractions;

namespace Hatband.Integrations.Steam;

public sealed class SteamGameInstallationProvider : IGameInstallationProvider
{
    private readonly ISteamInstalledGameScanner installedGameScanner;

    public SteamGameInstallationProvider(ISteamInstalledGameScanner installedGameScanner)
    {
        ArgumentNullException.ThrowIfNull(installedGameScanner);
        this.installedGameScanner = installedGameScanner;
    }

    public GameSourceId SourceId => GameSourceId.Steam;

    public async Task<IReadOnlyList<GameInstallationInfo>> ScanInstalledGamesAsync(
        CancellationToken cancellationToken = default)
    {
        var installedGames = await installedGameScanner.ScanAsync(cancellationToken);
        var installationInfo = new List<GameInstallationInfo>(installedGames.Count);
        foreach (var game in installedGames)
        {
            if (game.InstallDirectory is null)
            {
                throw new InvalidOperationException(
                    $"Steam reported installed game {game.AppId} without an installation directory.");
            }

            installationInfo.Add(new GameInstallationInfo
            {
                SourceGameId = game.AppId.ToString(CultureInfo.InvariantCulture),
                InstallDirectory = game.InstallDirectory
            });
        }

        return installationInfo;
    }
}
