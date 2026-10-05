using System.Globalization;
using Hatband.Core.Abstractions.Games;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Games;
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

    public async Task<IReadOnlyDictionary<string, GameInstallationInfo>> ScanInstalledGamesAsync(
        CancellationToken cancellationToken = default)
    {
        var installedGames = await installedGameScanner.ScanAsync(cancellationToken);
        var installationInfo = new Dictionary<string, GameInstallationInfo>(StringComparer.Ordinal);
        foreach (var game in installedGames)
        {
            if (game.InstallDirectory is null)
            {
                throw new InvalidOperationException(
                    $"Steam reported installed game {game.AppId} without an installation directory.");
            }

            var sourceGameId = game.AppId.ToString(CultureInfo.InvariantCulture);
            installationInfo.Add(sourceGameId, new GameInstallationInfo
            {
                InstallDirectory = game.InstallDirectory
            });
        }

        return installationInfo;
    }
}
