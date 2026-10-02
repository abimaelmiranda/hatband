using System.Globalization;
using Hatband.Core.Abstractions;
using Hatband.Core.Enums;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models;
using Hatband.Integrations.Steam.Abstractions;

namespace Hatband.Integrations.Steam;

public sealed class SteamGameManagementProvider : IGameManagementProvider
{
    private readonly IHostApplicationLauncher hostApplicationLauncher;
    private readonly ISteamInstalledGameScanner steamInstalledGameScanner;
    private readonly ISettingsStore settingsStore;

    public SteamGameManagementProvider(
        IHostApplicationLauncher hostApplicationLauncher,
        ISteamInstalledGameScanner steamInstalledGameScanner,
        ISettingsStore settingsStore)
    {
        ArgumentNullException.ThrowIfNull(hostApplicationLauncher);
        ArgumentNullException.ThrowIfNull(steamInstalledGameScanner);
        ArgumentNullException.ThrowIfNull(settingsStore);
        this.hostApplicationLauncher = hostApplicationLauncher;
        this.steamInstalledGameScanner = steamInstalledGameScanner;
        this.settingsStore = settingsStore;
    }

    public GameSourceId SourceId => GameSourceId.Steam;

    public async Task<IReadOnlyList<GameInstallLocation>> GetInstallLocationsAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();
        if (game.SourceId != GameSourceId.Steam || !await IsSilentModeEnabledAsync(cancellationToken))
        {
            return [];
        }

        var libraries = await steamInstalledGameScanner.GetLibraryLocationsAsync(cancellationToken);
        return libraries
            .Select(library => new GameInstallLocation(
                library.VolumeIndex.ToString(CultureInfo.InvariantCulture),
                $"{Path.GetFileName(library.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))} — {library.Path}"))
            .ToArray();
    }

    public async Task<GameManagementResult> InstallAsync(
        Game game,
        GameInstallLocation? location = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();
        if (!await IsSilentModeEnabledAsync(cancellationToken))
        {
            return await OpenSteamActionAsync(game, "install", cancellationToken);
        }

        if (location is not null && TryGetSteamAppId(game, out var appId) &&
            int.TryParse(location.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var volumeIndex) &&
            await hostApplicationLauncher.TryCloseSteamClientGracefullyAsync(cancellationToken) &&
            await hostApplicationLauncher.TryInstallSteamGameAsync(appId, volumeIndex, cancellationToken))
        {
            return GameManagementResult.SilentCommandStarted;
        }

        return await OpenSteamActionAsync(game, "install", cancellationToken, restoreHostAfterFallback: true);
    }

    public Task<GameManagementResult> UninstallAsync(Game game, CancellationToken cancellationToken = default)
    {
        return UninstallSteamGameAsync(game, cancellationToken);
    }

    public Task<GameManagementResult> LaunchAsync(Game game, CancellationToken cancellationToken = default)
    {
        return LaunchSteamGameAsync(game, cancellationToken);
    }

    public GameProcessWatchTarget? GetProcessWatchTarget(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (game.SourceId != GameSourceId.Steam ||
            !game.IsInstalled ||
            string.IsNullOrWhiteSpace(game.InstallDirectory) ||
            !Directory.Exists(game.InstallDirectory))
        {
            return null;
        }

        return new GameProcessWatchTarget(game.InstallDirectory);
    }

    private async Task<GameManagementResult> OpenSteamActionAsync(
        Game game,
        string action,
        CancellationToken cancellationToken,
        bool restoreHostAfterFallback = false)
    {
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryGetSteamAppId(game, out var appId))
        {
            return GameManagementResult.Unavailable;
        }

        var steamUri = new Uri($"steam://{action}/{appId.ToString(CultureInfo.InvariantCulture)}");
        if (await hostApplicationLauncher.TryOpenUriAsync(steamUri, cancellationToken))
        {
            return restoreHostAfterFallback
                ? GameManagementResult.FallbackProtocolOpened
                : GameManagementResult.ProtocolOpened;
        }

        if (!hostApplicationLauncher.IsSteamClientRunning() &&
            await hostApplicationLauncher.TryLaunchStoreClientAsync(GameSourceId.Steam, cancellationToken))
        {
            return restoreHostAfterFallback
                ? GameManagementResult.FallbackStoreClientOpened
                : GameManagementResult.StoreClientOpened;
        }

        return restoreHostAfterFallback
            ? GameManagementResult.FallbackUnavailable
            : GameManagementResult.Unavailable;
    }

    private async Task<GameManagementResult> LaunchSteamGameAsync(Game game, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryGetSteamAppId(game, out var appId))
        {
            return GameManagementResult.Unavailable;
        }

        if (await hostApplicationLauncher.TryLaunchSteamGameAsync(appId, cancellationToken))
        {
            return GameManagementResult.ProtocolOpened;
        }

        return await OpenSteamActionAsync(game, "run", cancellationToken);
    }

    private async Task<GameManagementResult> UninstallSteamGameAsync(
        Game game,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryGetSteamAppId(game, out var appId))
        {
            return GameManagementResult.Unavailable;
        }

        if (!await IsSilentModeEnabledAsync(cancellationToken))
        {
            return await OpenSteamActionAsync(game, "uninstall", cancellationToken);
        }

        if (await hostApplicationLauncher.TryCloseSteamClientGracefullyAsync(cancellationToken) &&
            await hostApplicationLauncher.TryUninstallSteamGameAsync(appId, cancellationToken))
        {
            return GameManagementResult.SilentCommandStarted;
        }

        return await OpenSteamActionAsync(game, "uninstall", cancellationToken, restoreHostAfterFallback: true);
    }

    private async Task<bool> IsSilentModeEnabledAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsStore.LoadAsync(cancellationToken);
        return settings.Steam.SilentModeEnabled;
    }

    private static bool TryGetSteamAppId(Game game, out uint appId)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (game.SourceId != GameSourceId.Steam)
        {
            appId = default;
            return false;
        }

        return uint.TryParse(game.SourceGameId, NumberStyles.None, CultureInfo.InvariantCulture, out appId);
    }
}
