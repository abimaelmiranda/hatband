using System.Globalization;
using Hatband.Core.Abstractions.Games;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Games;
using Hatband.Integrations.Steam.Abstractions;

namespace Hatband.Integrations.Steam;

public sealed class SteamGameInstallationProvider : IGameInstallationProvider
{
    private readonly ISteamInstalledGameScanner _installedGameScanner;

    public SteamGameInstallationProvider(ISteamInstalledGameScanner installedGameScanner)
    {
        ArgumentNullException.ThrowIfNull(installedGameScanner);
        _installedGameScanner = installedGameScanner;
    }

    public GameSourceId SourceId => GameSourceId.Steam;

    public async Task<IReadOnlyList<Game>> ScanInstalledGamesAsync(
        CancellationToken cancellationToken = default)
    {
        var installedGames = await _installedGameScanner.ScanAsync(cancellationToken);
        var games = new List<Game>(installedGames.Count);
        foreach (var installedGame in installedGames)
        {
            if (installedGame.InstallDirectory is not { } installDirectory)
            {
                throw new InvalidOperationException(
                    $"Steam reported installed game {installedGame.AppId} without an installation directory.");
            }

            games.Add(new Game
            {
                Name = installedGame.Name,
                SourceId = SourceId,
                SourceGameId = installedGame.AppId.ToString(CultureInfo.InvariantCulture),
                InstallationInfo = new GameInstallationInfo { InstallDirectory = installDirectory }
            });
        }

        return games;
    }
}
