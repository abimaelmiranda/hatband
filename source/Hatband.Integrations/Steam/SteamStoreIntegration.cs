using Hatband.Core.Abstractions;
using Hatband.Core.Abstractions.Authentication;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models;
using Hatband.Core.Models.Authentication;
using Hatband.Integrations.Steam.Abstractions;
using Hatband.Integrations.Steam.Models;

namespace Hatband.Integrations.Steam;

/// <summary>
/// Gets the user's Steam library through the Steam player service.
/// </summary>
public sealed class SteamStoreIntegration : IGameStoreIntegration, IQrCodeLoginProvider, IConnectorSessionProvider
{
    private readonly ISteamPlayerService steamPlayerService;
    private readonly ISteamInstalledGameScanner installedGameScanner;

    public SteamStoreIntegration(
        ISteamPlayerService steamPlayerService,
        ISteamInstalledGameScanner installedGameScanner)
    {
        this.steamPlayerService = steamPlayerService;
        this.installedGameScanner = installedGameScanner;
    }

    public GameSourceId SourceId => GameSourceId.Steam;

    public string DisplayName => "Steam";

    public ConnectorAccount? CurrentAccount => steamPlayerService.CurrentAccount;

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        steamPlayerService.Disconnect();
        return Task.CompletedTask;
    }

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
                    InstallDirectory = installedGame.InstallDirectory
                };
                continue;
            }

            gamesByAppId.TryAdd(installedGame.AppId, installedGame);
        }

        return gamesByAppId.Values
            .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(game => new Game
            {
                Name = game.Name,
                SourceId = SourceId,
                SourceGameId = game.AppId.ToString(),
                IsInstalled = game.IsInstalled,
                InstallDirectory = game.InstallDirectory,
                PlaytimeSeconds = game.PlaytimeSeconds,
                LastActivity = game.LastActivity
            })
            .ToArray();
    }
}
