using Hatband.Core.Abstractions.Authentication;
using Hatband.Core.Abstractions.Games;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Authentication;
using Hatband.Core.Models.Games;
using Hatband.Integrations.Steam.Abstractions;
using Hatband.Integrations.Steam.Models;
using Hatband.Integrations.Steam.PostInstall;

namespace Hatband.Integrations.Steam;

/// <summary>
/// Gets the user's Steam library through the Steam player service.
/// </summary>
public sealed class SteamStoreIntegration : IGameStoreIntegration, IQrCodeLoginProvider, IConnectorSessionProvider
{
    private readonly ISteamPlayerService steamPlayerService;
    private readonly ISteamInstalledGameScanner installedGameScanner;
    private readonly SteamGamePostInstallProvider postInstallProvider;

    public SteamStoreIntegration(
        ISteamPlayerService steamPlayerService,
        ISteamInstalledGameScanner installedGameScanner,
        SteamGamePostInstallProvider postInstallProvider)
    {
        this.steamPlayerService = steamPlayerService;
        this.installedGameScanner = installedGameScanner;
        this.postInstallProvider = postInstallProvider;
    }

    public string SourceId => GameSourceIds.Steam;

    public string DisplayName => "Steam";

    public ConnectorAccount? CurrentAccount => steamPlayerService.CurrentAccount;

    public ConnectorSession? CurrentSession => steamPlayerService.CurrentSession;

    public Task<bool> RestoreAsync(CancellationToken cancellationToken = default) =>
        steamPlayerService.RestoreAsync(cancellationToken);

    public Task DisconnectAsync(CancellationToken cancellationToken = default) =>
        steamPlayerService.DisconnectAsync(cancellationToken);

    public Task<IQrCodeLoginSession> BeginQrLoginAsync(CancellationToken cancellationToken = default)
    {
        return steamPlayerService.BeginQrLoginAsync(cancellationToken);
    }

    public Task<IReadOnlyList<Game>> GetLibraryAsync(CancellationToken cancellationToken = default)
    {
        return GetMergedLibraryAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<Game>> GetMergedLibraryAsync(CancellationToken cancellationToken)
    {
        var ownedGames = await steamPlayerService.GetOwnedGamesAsync(cancellationToken);
        var installedGames = await installedGameScanner.ScanAsync(cancellationToken);
        var gamesByAppId = new Dictionary<uint, SteamLibraryGame>();
        foreach (var ownedGame in ownedGames)
        {
            gamesByAppId.TryAdd(ownedGame.AppId, ownedGame);
        }

        foreach (var installedGame in installedGames)
        {
            if (gamesByAppId.TryGetValue(installedGame.AppId, out var ownedGame))
            {
                gamesByAppId[installedGame.AppId] = ownedGame with
                {
                    IsInstalled = true,
                    InstallDirectory = installedGame.InstallDirectory,
                    InstallManifestPath = installedGame.InstallManifestPath
                };
                continue;
            }

            gamesByAppId.TryAdd(installedGame.AppId, installedGame);
        }

        var games = new List<Game>(gamesByAppId.Count);
        foreach (var libraryGame in gamesByAppId.Values.OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var game = new Game
            {
                Name = libraryGame.Name,
                SourceId = SourceId,
                SourceGameId = libraryGame.AppId.ToString(),
                InstallationInfo = libraryGame.InstallDirectory is null
                    ? null
                    : new GameInstallationInfo { InstallDirectory = libraryGame.InstallDirectory },
                PlaytimeSeconds = libraryGame.PlaytimeSeconds,
                LastActivity = libraryGame.LastActivity
            };
            if (game.InstallationInfo is not null)
            {
                game.PostInstallPlan = await postInstallProvider.GetPlanAsync(game, libraryGame, cancellationToken);
            }

            games.Add(game);
        }

        return games;
    }
}
